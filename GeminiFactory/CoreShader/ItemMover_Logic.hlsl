// --- Splitter Logic ---
// 分流器决策：返回一个可用的方向向量，如果没有可用方向则返回 (0,0)
float2 UpdateSplitter(int idx, int2 pos, int itemID, inout int state)
{
    // 1. 解码状态
    // state 存储格式: [Direction (4 bits)] [Reserved Flag (1 bit)] [GridIndex (27 bits)]
    // 注意：GridIndex 实际上只用了低位，我们需要位移
    // MASK_INDEX 是 0xFFFFFFE0 (高 27 位)
    // MASK_DIR 是 0xF (低 4 位)
    
    int storedDir = state & MASK_DIR;
    int storedIdx = (state & MASK_INDEX) >> 5;
    
    // 检查是否是当前格子的决策
    // GetIdx 返回的是一维索引，通常小于 2^27
    bool hasDecision = (storedIdx == idx) && (storedDir != DIR_NONE);

    if (hasDecision) {
        return GetDir(storedDir);
    }

    // 2. 轮询出口
    int dirs[4] = { DIR_UP, DIR_RIGHT, DIR_DOWN, DIR_LEFT };
    int masks[4] = { 1, 8, 2, 4 };
    
    int mask = Map[idx].filter;
    int start = Map[idx].data % 4;
    if (start < 0) start += 4;

    for (int i = 0; i < 4; i++) {
        int k = (start + i) % 4;
        if ((mask & masks[k]) == 0) continue;

        float2 dirVec = GetDir(dirs[k]);
        int2 targetPos = pos + (int2)dirVec;
        
        if (!IsValid(targetPos.x, targetPos.y)) continue;
        
        int targetIdx = GetIdx(targetPos.x, targetPos.y);
        int targetType = Map[targetIdx].type;
        
        if (!IsBelt(targetType)) continue;
        
        float2 targetDir = GetDir(targetType);
        if (dot(targetDir, dirVec) < -0.9) continue;

        int occ = Grid[targetIdx];
        if (occ == 0 || occ == itemID + 1) {
            // 找到可用出口！
            int orig;
            InterlockedCompareExchange(Map[idx].data, start, (k + 1) % 4, orig);
            
            // 保存决策
            // 清除旧的方向和索引，保留 Reserved 标志
            state = (state & FLAG_RESERVED) | (idx << 5) | (dirs[k] & MASK_DIR);
            return dirVec;
        }
    }

    return float2(0, 0); // 全堵
}

// --- Elevator Logic ---
// 处理电梯内的移动和垂直传输
float2 UpdateElevator(int2 gridPos, float2 currentPos, float dt, int id, int type, int currentLayer, inout float targetHeight)
{
    float2 center = float2(gridPos.x, gridPos.y);
    float2 toCenter = center - currentPos;
    float distToCenter = length(toCenter);
    
    // 1. 移动阶段：未到中心
    // 强制向中心移动
    if (distToCenter > MoveSpeed * dt)
    {
        if (distToCenter > 0.0001) return currentPos + normalize(toCenter) * MoveSpeed * dt;
        return center;
    }
    
    // 2. 传输阶段：已到中心 (或非常接近)
    // 尝试垂直传输
    int nextLayer = -1;
    if (type >= 700 && type <= 704) nextLayer = currentLayer + 1; // Up
    else if (type >= 710 && type <= 714) nextLayer = currentLayer - 1; // Down
    
    if (nextLayer >= 0 && nextLayer < MAX_LAYERS)
    {
        int nextIdx = GetIdx(gridPos.x, gridPos.y, nextLayer);
        
        // 只要目标层未被占用，即可传输
        int orig;
        InterlockedCompareExchange(Grid[nextIdx], 0, id + 1, orig);
        
        if (orig == 0 || orig == id + 1)
        {
            // 传输成功
            targetHeight = (float)nextLayer;
        }
    }
    
    // 无论传输是否成功，都停在中心
    return center;
}

// --- Main Movement Logic ---
// 返回新的逻辑位置
float2 UpdateItem(int2 gridPos, float2 currentPos, float dt, int id, inout int state, inout float targetHeight)
{
    int currentLayer = (int)round(targetHeight);
    int gridIdx = GetIdx(gridPos.x, gridPos.y, currentLayer);
    
    int type = Map[gridIdx].type;
    float2 center = float2(gridPos.x, gridPos.y);
    
    // --- 1. 电梯逻辑 (独立分支) ---
    if (type >= 700) {
        return UpdateElevator(gridPos, currentPos, dt, id, type, currentLayer, targetHeight);
    }

    // --- 2. 确定移动方向 ---
    float2 dir = float2(0, 0);
    if (type == ID_SPLITTER) {
        dir = UpdateSplitter(gridIdx, gridPos, id, state);
    } else {
        dir = GetDir(type);
    }

    // 如果没有方向 (堵塞或无效)，停在中心
    if (dot(dir, dir) < ALIGN_THRESHOLD) { 
        if (type == ID_SPLITTER) {
             float2 toCenter = center - currentPos;
             if (dot(toCenter, toCenter) > 0.000001) return currentPos + normalize(toCenter) * MoveSpeed * dt;
        }
        return center;
    }

    // --- 3. 计算移动 ---
    float2 axis = abs(dir);
    float2 nextPos = currentPos + dir * MoveSpeed * dt;
    nextPos = nextPos * axis + center * (1.0 - axis); 

    // --- 4. 跨格检测 (Horizontal) ---
    float dist = dot(nextPos - center, dir);
    
    if (dist > BOUNDARY_THRESHOLD) {
        int2 nextGridPos = gridPos + (int2)round(dir);
        int nextLayer = currentLayer; 
        
        if (nextLayer < 0 || nextLayer >= MAX_LAYERS) return center + dir * BOUNDARY_THRESHOLD;

        int nextIdx = GetIdx(nextGridPos.x, nextGridPos.y, nextLayer);
        int nextType = Map[nextIdx].type;
        
        // [Strict Connectivity Check]
        // 1. 空地禁止移动
        if (nextType == 0) return center + dir * BOUNDARY_THRESHOLD;

        // 2. 非传送带/电梯禁止移动
        if (!IsBelt(nextType) && nextType < 700) { 
             return center + dir * BOUNDARY_THRESHOLD;
        }
        
        // 3. 逆行检测
        if (nextType != ID_SPLITTER && nextType < 700)
        {
            float2 nextDirVec = GetDir(nextType);
            if (dot(dir, nextDirVec) < -0.5)
                return center + dir * BOUNDARY_THRESHOLD;
        }

        // 4. 尝试抢占下一格
        int orig;
        InterlockedCompareExchange(Grid[nextIdx], 0, id + 1, orig);
        
        if (orig == 0 || orig == id + 1) {
            InterlockedCompareExchange(Grid[gridIdx], id + 1, 0, orig);
            
            float2 nextCenter = float2(nextGridPos.x, nextGridPos.y);
            float2 nextDir = GetDir(nextType);
            if (nextType >= 700) nextDir = GetDir(nextType % 10); 
            
            // [Optimization] Smooth transition for straight lines
            if (nextType != ID_SPLITTER && dot(dir, nextDir) > 0.9) {
                float overshoot = dist - BOUNDARY_THRESHOLD;
                return nextCenter - nextDir * CELL_HALF + nextDir * overshoot;
            }
            
            return nextCenter - nextDir * BOUNDARY_THRESHOLD;

        } else {
            // 抢占失败
            if (type == ID_SPLITTER) {
                state &= ~MASK_DIR; // 清除方向位
            }
            return center + dir * BOUNDARY_THRESHOLD;
        }
    }

    return nextPos;
}
