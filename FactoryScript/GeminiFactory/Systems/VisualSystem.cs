using UnityEngine;
using System.Collections.Generic;

namespace GeminFactory
{
    /// <summary>
    /// 视觉系统
    /// 负责渲染物品和更新传送带的视觉模型。
    /// </summary>
    [System.Serializable]
    public class VisualSystem
    {
        #region Fields & Dependencies
        // Settings
        private Mesh itemMesh;
        private Material itemMaterial;
        private BeltThemeSO beltTheme;

        private MapManager mapManager;
        private TransportSystem transportSystem;
        private ObjectPoolManager poolManager;
        #endregion

        #region Initialization
        /// <summary>
        /// 初始化视觉系统
        /// </summary>
        public void Initialize(GameContext context)
        {
            mapManager = context.MapManager;
            transportSystem = context.TransportSystem;
            poolManager = context.PoolManager;
            
            var config = context.GameConfig;
            itemMesh = config.itemMesh;
            itemMaterial = config.itemMaterial;
            beltTheme = config.beltTheme;

            GameEventBus.OnBeltChanged += UpdateBeltVisuals;
            GameEventBus.OnRenderUpdate += RenderItems;
        }

        public void Dispose()
        {
            GameEventBus.OnBeltChanged -= UpdateBeltVisuals;
            GameEventBus.OnRenderUpdate -= RenderItems;
        }
        #endregion

        #region Rendering
        /// <summary>
        /// 渲染所有物品 (使用 GPU Instancing)
        /// </summary>
        public void RenderItems()
        {
            if (transportSystem == null || transportSystem.buffers == null || transportSystem.buffers.ItemBuffer == null) return;
            
            itemMaterial.SetBuffer("items", transportSystem.buffers.ItemBuffer);
            Graphics.DrawMeshInstancedIndirect(itemMesh, 0, itemMaterial, new Bounds(Vector3.zero, Vector3.one * 1000), transportSystem.buffers.ArgsBuffer);
        }
        #endregion

        #region Belt Visualization
        /// <summary>
        /// 更新指定位置的传送带视觉模型
        /// <para>使用位掩码 (Bitmask) 简化形状匹配逻辑</para>
        /// </summary>
        public void UpdateBeltVisuals(Vector2Int pos)
        {
            if (!mapManager.IsValidGridPosition(pos)) return;
            int index = mapManager.GetIndex(pos);
            
            int data = mapManager.mapCells[index].type;
            // 0=空, >=MIN_BUILDING_ID=建筑，都不生成传送带
            if (data == 0 || data >= FactoryConstants.MIN_BUILDING_ID) return;
            
            // 额外检查：如果该位置被建筑占用 (即它是建筑的端口)，也不生成传送带模型
            if (mapManager.worldObjects.ContainsKey(index)) return;

            // 回收旧对象
            if (mapManager.beltObjects.ContainsKey(index) && mapManager.beltObjects[index] != null)
            {
                poolManager.RecycleBelt(mapManager.beltObjects[index]);
            }

            int myDir = mapManager.mapCells[index].type; // 1=Up, 2=Down, 3=Left, 4=Right
            List<int> inputDirs = GetInputDirections(pos);
            
            // 计算输入掩码 (相对于自身方向)
            // Bit 0: Back (1)
            // Bit 1: Left (2)
            // Bit 2: Right (4)
            int inputMask = 0;
            foreach (int inDir in inputDirs)
            {
                int relDir = GetRelativeDirection(myDir, inDir);
                if (relDir == 0) inputMask |= 1; // Back
                else if (relDir == 1) inputMask |= 2; // Left
                else if (relDir == 2) inputMask |= 4; // Right
            }

            bool isEnd = IsBeltEnd(pos, myDir);
            GameObject prefabToUse = GetBeltPrefab(inputMask, isEnd);

            Quaternion rotation = GetBeltRotation(myDir);
            GameObject newBelt = poolManager.SpawnBelt(prefabToUse, new Vector3(pos.x, 0, pos.y), rotation);
            mapManager.beltObjects[index] = newBelt;
        }

        /// <summary>
        /// 根据掩码获取对应的 Prefab
        /// </summary>
        GameObject GetBeltPrefab(int mask, bool isEnd)
        {
            if (beltTheme == null) return null;

            // Mask: 1=Back, 2=Left, 4=Right
            if (!isEnd)
            {
                switch (mask)
                {
                    case 0: return beltTheme.beltStartPrefab ?? beltTheme.beltEndPrefab;
                    case 1: return beltTheme.beltPrefab; // Straight
                    case 2: return beltTheme.beltTurnLeftPrefab ?? beltTheme.beltPrefab;
                    case 4: return beltTheme.beltTurnRightPrefab ?? beltTheme.beltPrefab;
                    case 3: return beltTheme.beltTLeftPrefab; // Back + Left
                    case 5: return beltTheme.beltTRightPrefab; // Back + Right
                    case 6: return beltTheme.beltTMergePrefab ?? beltTheme.beltCrossPrefab; // Left + Right
                    case 7: return beltTheme.beltCrossPrefab; // All
                    default: return beltTheme.beltPrefab;
                }
            }
            else
            {
                switch (mask)
                {
                    case 0: return beltTheme.beltSinglePrefab ?? beltTheme.beltEndPrefab;
                    case 1: return beltTheme.beltEndPrefab;
                    case 2: return beltTheme.beltEndTurnLeftPrefab ?? beltTheme.beltEndPrefab;
                    case 4: return beltTheme.beltEndTurnRightPrefab ?? beltTheme.beltEndPrefab;
                    case 3: return beltTheme.beltEndTLeftPrefab ?? beltTheme.beltEndPrefab;
                    case 5: return beltTheme.beltEndTRightPrefab ?? beltTheme.beltEndPrefab;
                    case 6: return beltTheme.beltEndMergePrefab ?? beltTheme.beltEndPrefab;
                    case 7: return beltTheme.beltEndCrossPrefab ?? beltTheme.beltEndPrefab;
                    default: return beltTheme.beltEndPrefab;
                }
            }
        }

        /// <summary>
        /// 获取输入方向相对于自身方向的关系
        /// 0=Back, 1=Left, 2=Right, -1=Invalid/Front
        /// </summary>
        int GetRelativeDirection(int myDir, int inDir)
        {
            // 1=Up, 2=Down, 3=Left, 4=Right
            // 简单的查找表逻辑
            if (myDir == 1) // Up
            {
                if (inDir == 1) return 0; // Back (输入也是Up，说明从后面来)
                if (inDir == 4) return 1; // Left (输入是Right，说明从左边来)
                if (inDir == 3) return 2; // Right (输入是Left，说明从右边来)
            }
            else if (myDir == 2) // Down
            {
                if (inDir == 2) return 0;
                if (inDir == 3) return 1;
                if (inDir == 4) return 2;
            }
            else if (myDir == 3) // Left
            {
                if (inDir == 3) return 0;
                if (inDir == 1) return 1;
                if (inDir == 2) return 2;
            }
            else if (myDir == 4) // Right
            {
                if (inDir == 4) return 0;
                if (inDir == 2) return 1;
                if (inDir == 1) return 2;
            }
            return -1;
        }
        #endregion

        #region Helpers
        List<int> GetInputDirections(Vector2Int pos)
        {
            List<int> inputs = new List<int>();
            CheckNeighborInput(pos + Vector2Int.up, 2, inputs);
            CheckNeighborInput(pos + Vector2Int.down, 1, inputs);
            CheckNeighborInput(pos + Vector2Int.left, 4, inputs);
            CheckNeighborInput(pos + Vector2Int.right, 3, inputs);
            return inputs;
        }

        void CheckNeighborInput(Vector2Int neighborPos, int requiredDir, List<int> inputs)
        {
            if (mapManager.IsValidGridPosition(neighborPos))
            {
                int idx = mapManager.GetIndex(neighborPos);
                if (mapManager.mapCells[idx].type == requiredDir)
                {
                    inputs.Add(requiredDir);
                }
            }
        }

        bool IsBeltEnd(Vector2Int pos, int myDir)
        {
            Vector2Int nextPos = pos + GetFlowVectorInt(myDir);
            if (!mapManager.IsValidGridPosition(nextPos)) return true;
            
            int nextIdx = mapManager.GetIndex(nextPos);
            int nextDir = mapManager.mapCells[nextIdx].type;

            if (nextDir == 0) return true;
            
            // 如果前方是建筑，检查是否有输入端口
            if (nextDir >= FactoryConstants.MIN_BUILDING_ID)
            {
                if (mapManager.worldObjects.TryGetValue(nextIdx, out GameObject obj))
                {
                    var info = obj.GetComponent<BuildingInfo>();
                    if (info != null && info.data != null)
                    {
                        // 计算 nextPos 相对于建筑原点的坐标
                        Vector2Int localPos = nextPos - info.origin;
                        
                        // 检查是否有输入端口位于该位置
                        foreach (var port in info.data.inputs)
                        {
                            if (port.position == localPos) return false; // 有输入口 -> 连接上了 -> 不是末端
                        }
                    }
                }
                return true; // 是建筑但没有输入口 -> 是末端
            }

            bool isOpposite = false;
            if (myDir == 1 && nextDir == 2) isOpposite = true;
            else if (myDir == 2 && nextDir == 1) isOpposite = true;
            else if (myDir == 3 && nextDir == 4) isOpposite = true;
            else if (myDir == 4 && nextDir == 3) isOpposite = true;

            return isOpposite;
        }

        Vector3 GetFlowVector(int dir)
        {
            switch (dir)
            {
                case 1: return new Vector3(0, 0, 1);
                case 2: return new Vector3(0, 0, -1);
                case 3: return new Vector3(-1, 0, 0);
                case 4: return new Vector3(1, 0, 0);
            }
            return Vector3.zero;
        }
        
        Vector2Int GetFlowVectorInt(int dir)
        {
            switch (dir)
            {
                case 1: return new Vector2Int(0, 1);
                case 2: return new Vector2Int(0, -1);
                case 3: return new Vector2Int(-1, 0);
                case 4: return new Vector2Int(1, 0);
            }
            return Vector2Int.zero;
        }

        Quaternion GetBeltRotation(int direction)
        {
            float yRot = 0;
            switch (direction)
            {
                case 1: yRot = 0; break;
                case 2: yRot = 180; break;
                case 3: yRot = -90; break;
                case 4: yRot = 90; break;
            }
            return Quaternion.Euler(90, yRot, 0);
        }
        #endregion
    }
}