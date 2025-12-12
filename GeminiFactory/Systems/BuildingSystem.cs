using UnityEngine;
using System.Collections.Generic;

namespace GeminFactory
{
    /// <summary>
    /// 建造系统
    /// 负责处理建筑的放置、拆除以及传送带路径的计算和建造。
    /// </summary>
    [System.Serializable]
    public class BuildingSystem
    {
        #region Fields & Dependencies
        private MapManager mapManager;
        private ObjectPoolManager poolManager;
        private TransportSystem transportSystem;

        private Transform factoryParent;
        private Transform shopParent;

        // 方向数组，用于简化邻居更新
        private readonly Vector2Int[] neighborOffsets = new Vector2Int[] 
        { 
            Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right 
        };
        #endregion

        #region Initialization
        public void Initialize(GameContext context)
        {
            mapManager = context.MapManager;
            poolManager = context.PoolManager;
            transportSystem = context.TransportSystem;
            factoryParent = context.FactoryParent;
            shopParent = context.ShopParent;

            // Subscribe to events
            GameEventBus.OnBuildRequest += PlaceBuilding;
            GameEventBus.OnBeltBuildRequest += BuildBeltPath;
            GameEventBus.OnDeleteRequest += DeleteObject;
        }

        public void Dispose()
        {
            GameEventBus.OnBuildRequest -= PlaceBuilding;
            GameEventBus.OnBeltBuildRequest -= BuildBeltPath;
            GameEventBus.OnDeleteRequest -= DeleteObject;
        }
        #endregion

        #region Building Placement
        /// <summary>
        /// 在指定位置放置建筑
        /// </summary>
        public void PlaceBuilding(Vector2Int originPos, BuildingDataSO data)
        {
            if (data == null || data.prefab == null) return;

            // 1. 检查并清理区域
            if (!ValidateAndClearArea(originPos, data)) return;

            // 2. 实例化建筑物体
            GameObject obj = InstantiateBuildingObject(originPos, data);
            
            // 3. 填充数据并记录引用
            UpdateMapDataForBuilding(originPos, data, obj);

            // 4. 发布建筑放置事件 (ProductionSystem 会监听此事件来更新工厂列表)
            GameEventBus.PublishBuildingPlaced(originPos, data);
        }

        private bool ValidateAndClearArea(Vector2Int originPos, BuildingDataSO data)
        {
            for (int x = 0; x < data.width; x++)
            {
                for (int y = 0; y < data.height; y++)
                {
                    Vector2Int p = originPos + new Vector2Int(x, y);
                    if (!mapManager.IsValidGridPosition(p)) return false;
                    
                    int idx = mapManager.GetIndex(p);
                    if (mapManager.mapCells[idx].type != 0)
                    {
                        DeleteObject(p);
                    }
                }
            }
            return true;
        }

        private GameObject InstantiateBuildingObject(Vector2Int originPos, BuildingDataSO data)
        {
            float offsetX = (data.width - 1) * 0.5f;
            float offsetZ = (data.height - 1) * 0.5f;
            Vector3 spawnPos = new Vector3(originPos.x + offsetX, 0.1f, originPos.y + offsetZ);
            
            Transform parent = (data.buildingType == BuildingType.Shop) ? shopParent : factoryParent;
            GameObject obj = Object.Instantiate(data.prefab, spawnPos, Quaternion.identity, parent);
            
            var info = obj.AddComponent<BuildingInfo>();
            info.data = data;
            info.origin = originPos; // 记录原点
            
            return obj;
        }

        private void UpdateMapDataForBuilding(Vector2Int originPos, BuildingDataSO data, GameObject obj)
        {
            int mapID = GetBuildingID(data.buildingType);

            for (int x = 0; x < data.width; x++)
            {
                for (int y = 0; y < data.height; y++)
                {
                    Vector2Int localPos = new Vector2Int(x, y);
                    Vector2Int p = originPos + localPos;
                    int idx = mapManager.GetIndex(p);
                    
                    // 记录对象引用
                    mapManager.worldObjects[idx] = obj;

                    // 设置地图数据 (端口或建筑ID)
                    // [修正] 对于分流器，强制忽略 SO 中的端口配置，直接进入 Else 分支处理
                    int inputIdx = (data.buildingType == BuildingType.Splitter) ? -1 : data.inputs.FindIndex(port => port.position == localPos);
                    int outputIdx = (data.buildingType == BuildingType.Splitter) ? -1 : data.outputs.FindIndex(port => port.position == localPos);

                    if (inputIdx != -1) 
                    {
                        // 使用特殊的输入口 ID (10 + direction) -> 11-14
                        mapManager.SetMapData(idx, 10 + data.inputs[inputIdx].direction);
                        
                        // 设置过滤数据
                        // 如果配方只有一个原料，则该输入口只允许该原料进入
                        if (data.recipe != null && data.recipe.inputs.Count == 1)
                        {
                            mapManager.SetFilterData(idx, data.recipe.inputs[0].item.id);
                        }
                        else
                        {
                            mapManager.SetFilterData(idx, 0); // 无限制 (多原料或无配方)
                        }
                    }
                    else if (outputIdx != -1) 
                    {
                        // 使用特殊的输出口 ID (14 + direction) -> 15-18
                        mapManager.SetMapData(idx, 14 + data.outputs[outputIdx].direction);
                    }
                    else 
                    {
                        if (data.buildingType == BuildingType.Splitter)
                        {
                            // 分流器特殊处理：忽略 SO 配置，使用默认逻辑 (一进三出)
                            // 分流器特殊处理：全方向输出 (15 = 1|2|4|8)
                            // Shader 会自动处理回流问题 (不向指向自己的传送带输出)
                            mapManager.SetFilterData(idx, 15);
                            mapManager.SetMapData(idx, FactoryConstants.ID_SPLITTER);
                        }
                        else
                        {
                            mapManager.SetMapData(idx, mapID);
                        }
                    }
                }
            }
            GameEventBus.PublishMapDataChanged();
        }

        int GetBuildingID(BuildingType type)
        {
            switch (type)
            {
                case BuildingType.Miner: return FactoryConstants.ID_MINER;
                case BuildingType.Processor: return FactoryConstants.ID_PROCESSOR;
                case BuildingType.Shop: return FactoryConstants.ID_SHOP;
                case BuildingType.Storage: return FactoryConstants.ID_STORAGE;
                case BuildingType.Splitter: return FactoryConstants.ID_SPLITTER;
                default: return FactoryConstants.ID_PROCESSOR;
            }
        }
        #endregion

        #region Object Deletion
        /// <summary>
        /// 删除指定位置的对象 (建筑或传送带)
        /// </summary>
        public void DeleteObject(Vector2Int pos)
        {
            int index = mapManager.GetIndex(pos);
            
            // 1. 尝试删除建筑
            if (mapManager.worldObjects.TryGetValue(index, out GameObject obj))
            {
                if (obj != null)
                {
                    DeleteBuildingInstance(obj, pos);
                    return;
                }
                else
                {
                    mapManager.worldObjects.Remove(index); // 清理空引用
                }
            }

            // 2. 尝试删除传送带
            if (mapManager.beltObjects.TryGetValue(index, out GameObject beltObj))
            {
                if (beltObj != null) poolManager.RecycleBelt(beltObj);
                mapManager.beltObjects.Remove(index);
            }

            // 3. 清除地图数据
            if (mapManager.mapCells[index].type != 0)
            {
                mapManager.SetMapData(index, 0);
                GameEventBus.PublishMapDataChanged();
                UpdateNeighbors(pos);
            }
        }

        /// <summary>
        /// 清空所有建筑和传送带 (用于加载存档前)
        /// </summary>
        public void ClearAll()
        {
            // 1. 收集所有需要删除的对象位置
            // 使用 HashSet 避免重复 (因为多格建筑会占用多个网格)
            HashSet<Vector2Int> objectsToDelete = new HashSet<Vector2Int>();

            // 收集建筑
            foreach (var kvp in mapManager.worldObjects)
            {
                if (kvp.Value != null)
                {
                    var info = kvp.Value.GetComponent<BuildingInfo>();
                    if (info != null)
                    {
                        objectsToDelete.Add(info.origin);
                    }
                    else
                    {
                        objectsToDelete.Add(mapManager.GetPosFromIndex(kvp.Key));
                    }
                }
            }

            // 收集传送带
            foreach (var kvp in mapManager.beltObjects)
            {
                objectsToDelete.Add(mapManager.GetPosFromIndex(kvp.Key));
            }

            // 2. 执行删除
            foreach (var pos in objectsToDelete)
            {
                DeleteObject(pos);
            }
            
            // 3. 强制清理残留数据 (双重保险)
            // 理论上 DeleteObject 应该处理干净，但为了确保加载新地图时没有残留，可以重置 MapData
            // 注意：这会清除所有物品占用数据，这正是我们想要的
            System.Array.Clear(mapManager.mapCells, 0, mapManager.mapCells.Length);
            System.Array.Clear(mapManager.gridOccupancyData, 0, mapManager.gridOccupancyData.Length);
            
            GameEventBus.PublishMapDataChanged();
        }

        private void DeleteBuildingInstance(GameObject obj, Vector2Int hitPos)
        {
            List<int> keysToRemove = new List<int>();
            var info = obj.GetComponent<BuildingInfo>();
            
            if (info != null && info.data != null)
            {
                // 优化：直接使用 origin 和尺寸来确定范围
                Vector2Int origin = info.origin;
                int w = info.data.width;
                int h = info.data.height;

                for (int x = 0; x < w; x++)
                {
                    for (int y = 0; y < h; y++)
                    {
                        Vector2Int p = origin + new Vector2Int(x, y);
                        if (mapManager.IsValidGridPosition(p))
                        {
                            int idx = mapManager.GetIndex(p);
                            
                            // 清理该位置的物品
                            int occupancy = mapManager.gridOccupancyData[idx];
                            if (occupancy > 0)
                            {
                                int itemIndex = occupancy - 1;
                                transportSystem.ConsumeItem(itemIndex, idx);
                            }

                            // 确认引用是否匹配，防止误删
                            if (mapManager.worldObjects.TryGetValue(idx, out GameObject o) && o == obj)
                            {
                                keysToRemove.Add(idx);
                            }
                        }
                    }
                }

                // 从工厂列表中移除
                // if (info.data.buildingType != BuildingType.Shop)
                // {
                //     // Handled by ProductionSystem via events
                // }
            }
            else
            {
                // Fallback: 遍历所有对象 (针对异常情况)
                foreach (var pair in mapManager.worldObjects)
                {
                    if (pair.Value == obj) keysToRemove.Add(pair.Key);
                }
            }

            foreach (int idx in keysToRemove)
            {
                // 如果该位置有数据，清除它
                if (mapManager.mapCells[idx].type != 0)
                {
                    mapManager.SetMapData(idx, 0);
                    mapManager.SetFilterData(idx, 0); // 清除过滤
                    UpdateNeighbors(mapManager.GetPosFromIndex(idx));
                }
                mapManager.worldObjects.Remove(idx);
            }
            
            Object.Destroy(obj);
            GameEventBus.PublishMapDataChanged();
            
            // 发布对象删除事件 (ProductionSystem 会监听此事件来更新工厂列表)
            // 注意：如果 info 为空 (异常情况)，我们可能无法获取 origin。
            // 但在这种情况下，ProductionSystem 也无法通过 pos 找到对应的 FactoryState，所以没关系。
            if (info != null)
            {
                GameEventBus.PublishObjectDeleted(info.origin);
            }
        }
        #endregion

        #region Belt Construction
        /// <summary>
        /// 建造传送带路径
        /// </summary>
        public void BuildBeltPath(Vector2Int start, Vector2Int end, bool isAlternatePath)
        {
            List<Vector2Int> points = CalculatePathPoints(start, end, isAlternatePath);
            HashSet<Vector2Int> dirtySet = new HashSet<Vector2Int>();

            for (int i = 0; i < points.Count; i++)
            {
                Vector2Int current = points[i];
                int dir = CalculateDirectionForPathIndex(points, i);
                
                if (SetBeltDirection(current, dir, false))
                {
                    dirtySet.Add(current);
                    foreach(var offset in neighborOffsets) dirtySet.Add(current + offset);
                }
            }

            foreach (var pos in dirtySet)
            {
                GameEventBus.PublishBeltChanged(pos);
            }
        }

        public bool SetBeltDirection(Vector2Int pos, int direction, bool updateVisuals = true)
        {
            int index = mapManager.GetIndex(pos);
            
            // 保护检查
            if (mapManager.mapCells[index].type >= FactoryConstants.MIN_BUILDING_ID) return false;
            if (mapManager.worldObjects.ContainsKey(index)) return false;

            // 仅当方向改变或没有传送带时更新
            if (mapManager.mapCells[index].type != direction || !mapManager.beltObjects.ContainsKey(index))
            {
                mapManager.SetMapData(index, direction);
                GameEventBus.PublishMapDataChanged();
                
                if (updateVisuals)
                {
                    GameEventBus.PublishBeltChanged(pos);
                    UpdateNeighbors(pos);
                }
                return true;
            }
            return false;
        }

        void UpdateNeighbors(Vector2Int pos)
        {
            foreach (var offset in neighborOffsets)
            {
                GameEventBus.PublishBeltChanged(pos + offset);
            }
        }
        #endregion

        #region Path Calculation Helpers
        /// <summary>
        /// 计算两点之间的曼哈顿路径点
        /// </summary>
        public List<Vector2Int> CalculatePathPoints(Vector2Int start, Vector2Int end, bool isAlternatePath)
        {
            List<Vector2Int> points = new List<Vector2Int>();
            int x = start.x;
            int y = start.y;

            if (!isAlternatePath)
            {
                // 先 X 后 Y
                while (x != end.x) { points.Add(new Vector2Int(x, y)); x += (end.x > x ? 1 : -1); }
                while (y != end.y) { points.Add(new Vector2Int(x, y)); y += (end.y > y ? 1 : -1); }
            }
            else
            {
                // 先 Y 后 X
                while (y != end.y) { points.Add(new Vector2Int(x, y)); y += (end.y > y ? 1 : -1); }
                while (x != end.x) { points.Add(new Vector2Int(x, y)); x += (end.x > x ? 1 : -1); }
            }

            points.Add(new Vector2Int(end.x, end.y));
            return points;
        }

        /// <summary>
        /// 计算路径上某一点的传送带方向
        /// </summary>
        public int CalculateDirectionForPathIndex(List<Vector2Int> points, int index)
        {
            if (index < points.Count - 1) return GetDirection(points[index], points[index + 1]);
            else if (index > 0) return GetDirection(points[index - 1], points[index]);
            return 4; // Default
        }

        int GetDirection(Vector2Int from, Vector2Int to)
        {
            if (to.x > from.x) return 4;
            if (to.x < from.x) return 3;
            if (to.y > from.y) return 1;
            if (to.y < from.y) return 2;
            return 4;
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
        #endregion
    }
}