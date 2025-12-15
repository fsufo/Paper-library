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
            // Temporary: Delete from all layers
            for (int layer = 0; layer < FactoryConstants.MAX_LAYERS; layer++)
            {
                int index = mapManager.GetIndex(pos.x, pos.y, layer);
                
                // [New] Check for Elevator Chain Deletion
                int type = mapManager.mapCells[index].type;
                if (type >= FactoryConstants.ID_ELEVATOR_UP_BASE)
                {
                    // 如果是电梯，触发链式删除
                    DeleteElevatorChain(new Vector3Int(pos.x, pos.y, layer), type);
                }
                else
                {
                    DeleteObjectAtIndex(index, pos);
                }
            }
        }

        private void DeleteElevatorChain(Vector3Int startPos, int startType)
        {
            // 确定搜索方向
            // 如果是 Up (700+)，我们向上搜索出口，向下搜索入口？
            // 或者简单点：只要是同位置、同类型的电梯，就全部删除。
            // 这样用户点击电梯的任何一部分，整根柱子都会消失。
            
            // 1. 向上搜索
            for (int h = startPos.z; h < FactoryConstants.MAX_LAYERS; h++)
            {
                int idx = mapManager.GetIndex(startPos.x, startPos.y, h);
                if (mapManager.mapCells[idx].type == startType)
                {
                    DeleteObjectAtIndex(idx, new Vector2Int(startPos.x, startPos.y));
                }
                else
                {
                    break; // 链条断裂
                }
            }
            
            // 2. 向下搜索
            for (int h = startPos.z - 1; h >= 0; h--)
            {
                int idx = mapManager.GetIndex(startPos.x, startPos.y, h);
                if (mapManager.mapCells[idx].type == startType)
                {
                    DeleteObjectAtIndex(idx, new Vector2Int(startPos.x, startPos.y));
                }
                else
                {
                    break; // 链条断裂
                }
            }
        }

        private void DeleteObjectAtIndex(int index, Vector2Int pos)
        {
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
            // 应该处理干净，但为了确保加载新地图时没有残留，可以重置 MapData
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

                            if (mapManager.worldObjects.TryGetValue(idx, out GameObject o) && o == obj)
                            {
                                keysToRemove.Add(idx);
                            }
                        }
                    }
                }
            }
            else
            {
                foreach (var pair in mapManager.worldObjects)
                {
                    if (pair.Value == obj) keysToRemove.Add(pair.Key);
                }
            }

            foreach (int idx in keysToRemove)
            {
                if (mapManager.mapCells[idx].type != 0)
                {
                    mapManager.SetMapData(idx, 0);
                    mapManager.SetFilterData(idx, 0);
                    UpdateNeighbors(mapManager.GetPosFromIndex(idx));
                }
                mapManager.worldObjects.Remove(idx);
            }
            
            Object.Destroy(obj);
            GameEventBus.PublishMapDataChanged();
            
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
        public void BuildBeltPath(Vector3Int start, Vector3Int end, bool isAlternatePath)
        {
            List<Vector3Int> points = CalculatePathPoints(start, end, isAlternatePath);
            HashSet<Vector2Int> dirtySet = new HashSet<Vector2Int>();

            for (int i = 0; i < points.Count; i++)
            {
                Vector3Int current = points[i];
                Vector2Int pos2D = new Vector2Int(current.x, current.y);
                
                // 计算方向
                int dir = CalculateDirectionForPathIndex(points, i);
                
                // 默认类型为普通传送带
                int type = dir; 
                
                // 检查是否需要生成电梯 (检测到与下一个点的高度差)
                if (i < points.Count - 1)
                {
                    Vector3Int next = points[i + 1];
                    
                    // [Multi-layer Elevator Logic]
                    // 如果下一个点高度不同，说明这里是垂直连接的起点
                    if (next.z != current.z)
                    {
                        bool isUp = next.z > current.z;
                        int elevatorBase = isUp ? FactoryConstants.ID_ELEVATOR_UP_BASE : FactoryConstants.ID_ELEVATOR_DOWN_BASE;
                        type = elevatorBase + dir;

                        // 自动填充中间层 (确保电梯是连续的)
                        // 如果是向上：填充 (current.z, next.z) 之间的层
                        // 如果是向下：填充 (next.z, current.z) 之间的层
                        int step = isUp ? 1 : -1;
                        
                        for (int h = current.z + step; h != next.z; h += step)
                        {
                            Vector3Int midPos = new Vector3Int(current.x, current.y, h);
                            // 中间层也是电梯管道，方向与入口一致
                            if (SetBeltDirection(midPos, type, false))
                            {
                                dirtySet.Add(pos2D);
                            }
                        }
                    }
                }

                if (SetBeltDirection(current, type, false))
                {
                    dirtySet.Add(pos2D);
                    foreach(var offset in neighborOffsets) dirtySet.Add(pos2D + offset);
                }
            }

            foreach (var pos in dirtySet)
            {
                GameEventBus.PublishBeltChanged(pos);
            }
        }
        
        public bool SetBeltDirection(Vector3Int pos, int type, bool updateVisuals = true)
        {
            // Bounds Check
            if (pos.x < 0 || pos.x >= mapManager.mapWidth || 
                pos.y < 0 || pos.y >= mapManager.mapHeight || 
                pos.z < 0 || pos.z >= FactoryConstants.MAX_LAYERS)
            {
                return false;
            }

            int index = mapManager.GetIndex(pos);
            
            // 保护检查
            if (mapManager.mapCells[index].type >= FactoryConstants.MIN_BUILDING_ID && mapManager.mapCells[index].type < FactoryConstants.ID_SPLITTER) return false;
            if (mapManager.worldObjects.ContainsKey(index)) return false;

            // 仅当类型改变或没有传送带时更新
            int currentType = mapManager.mapCells[index].type;

            if (currentType != type || !mapManager.beltObjects.ContainsKey(index))
            {
                mapManager.SetMapData(index, type);
                GameEventBus.PublishMapDataChanged();
                
                if (updateVisuals)
                {
                    Vector2Int pos2D = new Vector2Int(pos.x, pos.y);
                    GameEventBus.PublishBeltChanged(pos2D);
                    UpdateNeighbors(pos2D);
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
        /// 计算两点之间的曼哈顿路径点 (3D)
        /// <para>如果起点和终点高度不同，会在起点位置生成一个垂直转折点。</para>
        /// </summary>
        public List<Vector3Int> CalculatePathPoints(Vector3Int start, Vector3Int end, bool isAlternatePath)
        {
            List<Vector3Int> points = new List<Vector3Int>();
            int x = start.x;
            int y = start.y;
            int z = end.z; // 路径主体高度跟随终点 (当前滚轮高度)

            // 1. 生成 2D 路径
            List<Vector2Int> path2D = new List<Vector2Int>();
            if (!isAlternatePath)
            {
                while (x != end.x) { path2D.Add(new Vector2Int(x, y)); x += (end.x > x ? 1 : -1); }
                while (y != end.y) { path2D.Add(new Vector2Int(x, y)); y += (end.y > y ? 1 : -1); }
            }
            else
            {
                while (y != end.y) { path2D.Add(new Vector2Int(x, y)); y += (end.y > y ? 1 : -1); }
                while (x != end.x) { path2D.Add(new Vector2Int(x, y)); x += (end.x > x ? 1 : -1); }
            }
            path2D.Add(new Vector2Int(end.x, end.y));

            // 2. 转换为 3D 路径
            for (int i = 0; i < path2D.Count; i++)
            {
                // 特殊处理：起点变层逻辑
                // 如果是路径的第一个点，且起点高度与终点高度不同
                if (i == 0 && start.z != end.z)
                {
                    // 添加起点 (原始高度)
                    points.Add(new Vector3Int(path2D[i].x, path2D[i].y, start.z));
                    
                    // 立即添加一个同位置但高度为目标高度的点
                    // 这会触发 BuildBeltPath 中的电梯生成逻辑
                    points.Add(new Vector3Int(path2D[i].x, path2D[i].y, end.z));
                }
                else
                {
                    // 其余点都在目标高度
                    points.Add(new Vector3Int(path2D[i].x, path2D[i].y, z));
                }
            }
            
            return points;
        }

        /// <summary>
        /// 计算路径上某一点的传送带方向
        /// </summary>
        public int CalculateDirectionForPathIndex(List<Vector3Int> points, int index)
        {
            // 如果当前点和下一点位置相同 (垂直连接)，方向应该跟随后续路径
            if (index < points.Count - 1)
            {
                Vector3Int current = points[index];
                Vector3Int next = points[index + 1];
                
                if (current.x == next.x && current.y == next.y)
                {
                    // 垂直连接，寻找下一个位置不同的点来确定方向
                    for (int k = index + 1; k < points.Count; k++)
                    {
                        if (points[k].x != current.x || points[k].y != current.y)
                        {
                            return GetDirection(current, points[k]);
                        }
                    }
                    // 如果后面没有位置不同的点 (即路径就在原地垂直)，则保持默认或跟随上一个
                    if (index > 0) return CalculateDirectionForPathIndex(points, index - 1);
                    return 4; // Default
                }
                
                return GetDirection(current, next);
            }
            else if (index > 0) 
            {
                // 末端点，跟随上一个点的方向
                // 如果上一个点是垂直连接点，递归查找
                return CalculateDirectionForPathIndex(points, index - 1);
            }
            return 4; // Default
        }

        int GetDirection(Vector3Int from, Vector3Int to)
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