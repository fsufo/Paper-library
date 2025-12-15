// 分流器逻辑：只负责决策，不负责移动
// 依赖：IsBlocked (在 ItemMover_Logic.hlsl 中定义)
void ProcessSplitter(int gridIndex, int2 gridPos, float2 centerPos, float2 currentPos, uint itemIndex, inout float2 moveDir, inout bool isAligning, inout int localExtraData)
{
    // 1. 状态检查
    int storedGridIndex = localExtraData & 0x0FFFFFFF;
    bool hasDecision = (storedGridIndex == gridIndex);
    
    // 2. 决策逻辑
    bool needDecision = !hasDecision;
    
    if (hasDecision)
    {
        // 检查当前决策是否有效
        int targetDirVal = (localExtraData >> 28) & 0xF;
        float2 dirVec = GetDir(targetDirVal);
        int2 targetGrid = gridPos + (int2)dirVec;
        
        if (IsValidGrid(targetGrid.x, targetGrid.y))
        {
            int tIdx = GetGridIndex(targetGrid.x, targetGrid.y);
            int occupier = GridOccupancy[tIdx];
            // 如果目标被占且不是我，说明堵了，需要重选
            if (occupier != 0 && occupier != (int)itemIndex + 1)
            {
                hasDecision = false; // 强制重选
            }
        }
        else hasDecision = false; // 出界
    }

    // 如果已决策，检查是否堵塞
    if (hasDecision)
    {
        // [Fix] 只有当物品还在中心附近时，才允许反悔 (重选)
        // 一旦物品走远了 (开始 Outbound)，就必须坚持走到底，防止在边缘和中心之间抽搐
        float dist = length(currentPos - centerPos);
        if (dist < 0.05)
        {
            int targetDirVal = (localExtraData >> 28) & 0xF;
            float2 dirVec = GetDir(targetDirVal);
            int2 targetGrid = gridPos + (int2)dirVec;
            
            if (IsValidGrid(targetGrid.x, targetGrid.y))
            {
                int tIdx = GetGridIndex(targetGrid.x, targetGrid.y);
                int occupier = GridOccupancy[tIdx];
                // 如果目标被占且不是我，说明堵了，尝试重选
                if (occupier != 0 && occupier != (int)itemIndex + 1)
                {
                    hasDecision = false; 
                }
            }
            else hasDecision = false;
        }
    }

    if (!hasDecision)
    {
        int dirIDs[4] = { BELT_UP, BELT_RIGHT, BELT_DOWN, BELT_LEFT };
        int maskBits[4] = { 1, 8, 2, 4 };
        float2 dirVecs[4] = { float2(0, 1), float2(1, 0), float2(0, -1), float2(-1, 0) };
        int mask = MapGrid[gridIndex].filterID;
        int startOffset = MapGrid[gridIndex].reserved1 % 4;
        if (startOffset < 0) startOffset += 4;

        int selectedDir = 0;
        int foundIndex = -1;

        for (int i = 0; i < 4; i++)
        {
            int idx = (startOffset + i) % 4;
            if ((mask & maskBits[idx]) == 0) continue;

            int2 targetGrid = gridPos + (int2)dirVecs[idx];
            if (!IsValidGrid(targetGrid.x, targetGrid.y)) continue;
            
            int tIdx = GetGridIndex(targetGrid.x, targetGrid.y);
            int nextType = MapGrid[tIdx].type;
            
            // [Fix] 有效性检查：必须是传送带组件
            if (!IsBelt(nextType)) continue;

            float2 nextDir = GetDir(nextType);
            
            // 回流检查
            if (dot(nextDir, dirVecs[idx]) < -0.9) continue;

            // 占用检查：只要是空的，或者是我自己，就选它
            int occupier = GridOccupancy[tIdx];
            if (occupier == 0 || occupier == (int)itemIndex + 1)
            {
                selectedDir = dirIDs[idx];
                foundIndex = idx;
                break;
            }
        }

        if (selectedDir != 0)
        {
            localExtraData = (selectedDir << 28) | (gridIndex & 0x0FFFFFFF);
            int original;
            InterlockedCompareExchange(MapGrid[gridIndex].reserved1, startOffset, (foundIndex + 1) % 4, original);
        }
        else
        {
            localExtraData = -1;
        }
    }
}