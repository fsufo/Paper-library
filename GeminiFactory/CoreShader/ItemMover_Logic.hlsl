// --- Core Logic Functions ---

// 极简阻挡检测：只关心能不能进
bool IsBlocked(int2 currentGrid, int2 targetGrid, float2 moveDir, int myID)
{
    if (!IsValidGrid(targetGrid.x, targetGrid.y)) return true;

    int targetIndex = GetGridIndex(targetGrid.x, targetGrid.y);
    int targetType = MapGrid[targetIndex].type;

    // 1. 物理占用检查
    int occupier = GridOccupancy[targetIndex];
    if (occupier != 0 && occupier != myID + 1) 
    {
        // 分流器：必须完全空闲 (防重叠)
        if (targetType == ID_SPLITTER) return true;

        // 传送带：允许跟车 (只要前车在动且不逆行)
        int otherID = occupier - 1;
        if (otherID >= 0 && otherID < maxItems && items[otherID].isActive != 0)
        {
            float2 otherVel = items[otherID].velocity;
            if (dot(otherVel, moveDir) <= 0.01) return true; // 前车不动或逆行 -> 堵
        }
        else return true; // 无效占用 -> 堵
    }

    // 2. 逻辑过滤 (掩码/类型)
    int filterID = MapGrid[targetIndex].filterID;
    if (targetType != ID_SPLITTER && filterID != 0 && filterID != items[myID].itemID) return true;

    // 3. 类型兼容性
    bool isBelt = (targetType >= BELT_UP && targetType <= BELT_RIGHT);
    bool isInput = (targetType >= BELT_INPUT_UP && targetType <= BELT_INPUT_RIGHT);
    bool isOutput = (targetType >= BELT_OUTPUT_UP && targetType <= BELT_OUTPUT_RIGHT);
    bool isSplitter = (targetType == ID_SPLITTER);
    if (!isBelt && !isInput && !isOutput && !isSplitter) return true;

    // 4. 端口方向严格检查
    if (isInput || isOutput)
    {
        float2 portDir = GetBeltDirection(targetType);
        if (dot(moveDir, portDir) < 0.9) return true;
    }

    // 5. 汇入避让 (简化版：只看距离)
    // 检查所有邻居，如果有竞争者且比我更近/ID更小，我就让
    float2 targetCenter = float2(targetGrid.x, targetGrid.y);
    int2 offsets[4] = { int2(0, 1), int2(0, -1), int2(-1, 0), int2(1, 0) };
    
    for (int i = 0; i < 4; i++)
    {
        int2 neighbor = targetGrid + offsets[i];
        if (neighbor.x == currentGrid.x && neighbor.y == currentGrid.y) continue;
        if (!IsValidGrid(neighbor.x, neighbor.y)) continue;

        int nIdx = GetGridIndex(neighbor.x, neighbor.y);
        int nOccupier = GridOccupancy[nIdx];
        
        if (nOccupier != 0 && nOccupier != myID + 1)
        {
            // 检查邻居是否指向目标
            int nType = MapGrid[nIdx].type;
            float2 nDir = GetBeltDirection(nType);
            int2 nNext = neighbor + (int2)round(nDir);
            
            if (nNext.x == targetGrid.x && nNext.y == targetGrid.y)
            {
                // 发现竞争者，仲裁
                int otherID = nOccupier - 1;
                if (otherID >= 0 && otherID < maxItems)
                {
                    float myDist = distance(items[myID].position, targetCenter);
                    float otherDist = distance(items[otherID].position, targetCenter);
                    
                    if (otherDist < myDist - 0.01) return true; // 他近，我让
                    if (myDist < otherDist - 0.01) continue;    // 我近，我不让
                    if (otherID < myID) return true;            // 距离一样，ID小先走
                }
            }
        }
    }

    return false;
}

// 分流器逻辑：极简版
void ProcessSplitter(int gridIndex, int2 gridPos, float2 centerPos, float2 currentPos, uint itemIndex, inout float2 moveDir, inout bool isAligning, inout int localExtraData)
{
    // 1. 状态检查
    int storedGridIndex = localExtraData & 0x0FFFFFFF;
    bool hasDecision = (storedGridIndex == gridIndex);
    float dist = length(currentPos - centerPos);

    // 2. 决策逻辑 (未决策且到达中心)
    if (!hasDecision)
    {
        if (dist < 0.05)
        {
            // 扫描所有畅通出口
            int availableDirs[4];
            int availableCount = 0;
            int mask = MapGrid[gridIndex].filterID;
            
            // 顺时针: UP(1), RIGHT(8), DOWN(2), LEFT(4)
            int dirIDs[4] = { BELT_UP, BELT_RIGHT, BELT_DOWN, BELT_LEFT };
            int maskBits[4] = { 1, 8, 2, 4 };
            float2 dirVecs[4] = { float2(0, 1), float2(1, 0), float2(0, -1), float2(-1, 0) };

            for (int i = 0; i < 4; i++)
            {
                if ((mask & maskBits[i]) == 0) continue; // 掩码过滤

                int2 targetGrid = gridPos + (int2)dirVecs[i];
                
                // 检查是否回流 (输入口)
                if (IsValidGrid(targetGrid.x, targetGrid.y))
                {
                    int tIdx = GetGridIndex(targetGrid.x, targetGrid.y);
                    float2 tDir = GetBeltDirection(MapGrid[tIdx].type);
                    if (dot(tDir, dirVecs[i]) < -0.9) continue; // 是输入口，跳过
                }

                // 检查是否堵塞 (关键：只选通的路)
                if (!IsBlocked(gridPos, targetGrid, dirVecs[i], (int)itemIndex))
                {
                    availableDirs[availableCount++] = dirIDs[i];
                }
            }

            if (availableCount > 0)
            {
                // 取号并分配
                int ticket;
                InterlockedAdd(MapGrid[gridIndex].reserved1, 1, ticket);
                int selectedDir = availableDirs[ticket % availableCount];
                
                // 锁定状态
                localExtraData = (selectedDir << 28) | (gridIndex & 0x0FFFFFFF);
                hasDecision = true;
            }
            else
            {
                // 全堵：停下，下一帧重试
                moveDir = float2(0, 0);
                return;
            }
        }
        else
        {
            // 没到中心：走过去
            moveDir = normalize(-currentPos + centerPos);
            isAligning = true;
            return;
        }
    }

    // 3. 执行逻辑 (已决策)
    if (hasDecision)
    {
        int targetDirVal = (localExtraData >> 28) & 0xF;
        float2 dirVec = GetBeltDirection(targetDirVal);
        int2 targetGrid = gridPos + (int2)dirVec;

        // 动态重决策：如果锁定的路突然堵了，且还在中心，就反悔
        if (dist < 0.1 && IsBlocked(gridPos, targetGrid, dirVec, (int)itemIndex))
        {
            localExtraData = -1; // 重置
            items[itemIndex].extraData = -1; // 双重保险
            moveDir = float2(0, 0);
        }
        else
        {
            moveDir = dirVec;
        }
    }
}

// 输入口逻辑
void ProcessInputPort(float2 toCenter, float2 centerPos, inout float2 moveDir, inout bool isAligning, inout float2 currentPos)
{
    if (length(toCenter) < 0.05) 
    {
        currentPos = centerPos;
        moveDir = float2(0,0);
    }
    else
    {
        moveDir = normalize(toCenter);
        isAligning = true;
    }
}

// 传送带对齐逻辑
void ProcessBeltAlignment(float2 targetDir, float2 toCenter, float2 centerPos, inout float2 moveDir, inout bool isAligning, inout float2 currentPos)
{
    if (length(targetDir) > 0.1)
    {
        if (abs(targetDir.x) > 0.5) 
        {
            if (abs(toCenter.y) > 0.02) { moveDir = float2(0, sign(toCenter.y)); isAligning = true; }
            else currentPos.y = centerPos.y;
        }
        else if (abs(targetDir.y) > 0.5) 
        {
            if (abs(toCenter.x) > 0.02) { moveDir = float2(sign(toCenter.x), 0); isAligning = true; }
            else currentPos.x = centerPos.x;
        }
    }
}

// 物理移动计算
float2 CalculateNextPosition(int2 gridPos, float2 currentPos, float2 moveDir, float moveSpeed, float dt, bool isAligning, float2 centerPos, float2 toCenter, int myID)
{
    float2 nextPos = currentPos;
    bool blocked = false;

    if (length(moveDir) > 0.1)
    {
        if (!isAligning)
        {
            int2 forwardGrid = gridPos + (int2)moveDir;
            if (IsBlocked(gridPos, forwardGrid, moveDir, myID)) blocked = true;
        }

        nextPos = currentPos + moveDir * moveSpeed * dt;
        
        if (isAligning)
        {
            float2 newToCenter = centerPos - nextPos;
            if (toCenter.x * newToCenter.x < 0 || toCenter.y * newToCenter.y < 0) nextPos = centerPos;
        }
        else if (blocked)
        {
             if (moveDir.x > 0.5) nextPos.x = min(nextPos.x, (float)gridPos.x);
             else if (moveDir.x < -0.5) nextPos.x = max(nextPos.x, (float)gridPos.x);
             else if (moveDir.y > 0.5) nextPos.y = min(nextPos.y, (float)gridPos.y);
             else if (moveDir.y < -0.5) nextPos.y = max(nextPos.y, (float)gridPos.y);
        }

        // 跨网格检查
        int nextGridX = (int)round(nextPos.x);
        int nextGridY = (int)round(nextPos.y);
        
        if (nextGridX != gridPos.x || nextGridY != gridPos.y)
        {
            if (IsValidGrid(nextGridX, nextGridY))
            {
                int nextIndex = GetGridIndex(nextGridX, nextGridY);
                int occupier = GridOccupancy[nextIndex];
                if (occupier != 0 && occupier != myID + 1) 
                {
                    if (moveDir.x > 0.5) nextPos.x = (float)gridPos.x + 0.49;
                    else if (moveDir.x < -0.5) nextPos.x = (float)gridPos.x - 0.49;
                    else if (moveDir.y > 0.5) nextPos.y = (float)gridPos.y + 0.49;
                    else if (moveDir.y < -0.5) nextPos.y = (float)gridPos.y - 0.49;
                }
            }
            else 
            {
                nextPos = currentPos; 
            }
        }
    }
    return nextPos;
}
