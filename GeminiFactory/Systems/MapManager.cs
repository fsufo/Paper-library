using UnityEngine;
using System.Collections.Generic;

namespace GeminFactory
{
    /// <summary>
    /// 地图管理器
    /// 负责维护地图网格数据、对象引用和坐标转换。
    /// </summary>
    [System.Serializable]
    public class MapManager
    {
        #region Settings
        // Settings (由 Manager 传递或直接设置)
        public int mapWidth = 100;
        public int mapHeight = 100;
        #endregion

        #region Data Buffers
        /// <summary>
        /// 网格占用数据 (用于物品碰撞检测)
        /// 0 = 空, >0 = 物品ID+1
        /// </summary>
        public int[] gridOccupancyData { get; private set; } 

        /// <summary>
        /// 地图单元格数据 (用于上传 GPU)
        /// </summary>
        public MapCell[] mapCells { get; private set; }
        #endregion

        #region Game State
        /// <summary>
        /// 建筑对象字典 (Key: GridIndex, Value: GameObject)
        /// </summary>
        public Dictionary<int, GameObject> worldObjects { get; private set; } = new Dictionary<int, GameObject>();
        
        /// <summary>
        /// 传送带对象字典 (Key: GridIndex, Value: GameObject)
        /// </summary>
        public Dictionary<int, GameObject> beltObjects { get; private set; } = new Dictionary<int, GameObject>();
        #endregion

        #region Initialization
        /// <summary>
        /// 初始化地图数据
        /// </summary>
        public void Initialize(int width, int height)
        {
            mapWidth = width;
            mapHeight = height;
            gridOccupancyData = new int[mapWidth * mapHeight];
            mapCells = new MapCell[mapWidth * mapHeight]; // Init cells
        }
        
        /// <summary>
        /// 更新网格占用数据 (通常由 SimulationSystem 回读 GPU 数据后调用)
        /// </summary>
        public void UpdateGridOccupancy(int[] newData)
        {
            if (newData.Length == gridOccupancyData.Length)
            {
                // 直接拷贝数据，锁定逻辑已在 TransportSystem 中处理
                System.Array.Copy(newData, gridOccupancyData, newData.Length);
            }
        }
        
        /// <summary>
        /// 设置过滤数据
        /// </summary>
        public void SetFilterData(int index, int itemID)
        {
            if (index >= 0 && index < mapCells.Length)
            {
                mapCells[index].filterID = itemID;
            }
        }
        #endregion

        #region Coordinate Conversion
        /// <summary>
        /// 检查坐标是否在地图范围内
        /// </summary>
        public bool IsValidGridPosition(Vector2Int pos)
        {
            return pos.x >= 0 && pos.x < mapWidth && pos.y >= 0 && pos.y < mapHeight;
        }

        /// <summary>
        /// 将二维坐标转换为一维索引
        /// </summary>
        public int GetIndex(Vector2Int pos)
        {
            return pos.y * mapWidth + pos.x;
        }

        /// <summary>
        /// 将一维索引转换为二维坐标
        /// </summary>
        public Vector2Int GetPosFromIndex(int index)
        {
            return new Vector2Int(index % mapWidth, index / mapWidth);
        }

        /// <summary>
        /// 将世界坐标转换为网格坐标
        /// </summary>
        public Vector2Int WorldToGrid(Vector3 worldPos)
        {
            return new Vector2Int(Mathf.RoundToInt(worldPos.x), Mathf.RoundToInt(worldPos.z));
        }
        #endregion

        #region Data Manipulation
        /// <summary>
        /// 设置地图数据
        /// </summary>
        public void SetMapData(int index, int value)
        {
            if (index >= 0 && index < mapCells.Length)
            {
                mapCells[index].type = value;
            }
        }
        #endregion
    }
}