using UnityEngine;
using System.Runtime.InteropServices;

namespace GeminFactory
{
    /// <summary>
    /// 物品数据结构 (与 Compute Shader 保持内存对齐)
    /// </summary>
    [System.Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct ItemData
    {
        public Vector2 position;
        public Vector2 velocity;
        public Vector4 color;
        public int isActive;
        public int price;
        public int itemID;
        public int extraData; // [Rename] padding -> extraData, 用于存储分流器状态
    }

    /// <summary>
    /// 地图单元格数据结构 (用于 ComputeBuffer)
    /// </summary>
    [System.Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct MapCell
    {
        public int type;      // 地图类型 (传送带方向/建筑ID)
        public int filterID;  // 过滤ID
        public int reserved1; // 预留
        public int reserved2; // 预留
    }

    /// <summary>
    /// 工厂运行时状态
    /// </summary>
    public class FactoryState
    {
        public float progressTimer;
        public System.Collections.Generic.Dictionary<int, int> inventory = new System.Collections.Generic.Dictionary<int, int>(); // Key: ItemID, Value: Count
        public bool isProcessing;
    }

    #region Enums
    /// <summary>
    /// 当前建造模式
    /// </summary>
    public enum BuildMode 
    { 
        None, 
        Belt,       // 传送带模式
        Factory,    // 工厂建造模式 (Miner/Processor)
        Shop,       // 商店建造模式
        Delete      // 拆除模式
    }

    /// <summary>
    /// 建筑类型枚举
    /// </summary>
    public enum BuildingType 
    { 
        Miner,      // 矿场：无输入，产出矿物
        Processor,  // 加工厂：有输入，有输出
        Shop,       // 商店：只进不出，卖钱
        Storage,    // 箱子：储存物品
        Splitter    // 分流器：一进多出
    }
    #endregion

    #region Constants
    /// <summary>
    /// 全局常量定义
    /// </summary>
    public static class FactoryConstants
    {
        // 基础 ID 定义 (对应 mapData 中的值)
        // Shop 必须是 100 以兼容 Shader (如果 Shader 硬编码了 100)
        public const int ID_SHOP = 100;
        public const int ID_MINER = 200;
        public const int ID_PROCESSOR = 300;
        public const int ID_STORAGE = 400;
        
        // 输入口 ID (11-14)
        public const int ID_INPUT_UP = 11;
        public const int ID_INPUT_DOWN = 12;
        public const int ID_INPUT_LEFT = 13;
        public const int ID_INPUT_RIGHT = 14;
        
        // 输出口 ID (15-18)
        public const int ID_OUTPUT_UP = 15;
        public const int ID_OUTPUT_DOWN = 16;
        public const int ID_OUTPUT_LEFT = 17;
        public const int ID_OUTPUT_RIGHT = 18;
        
        // 最小建筑 ID (用于判断是否是建筑，>= 100 为建筑，< 100 为传送带或空)
        public const int MIN_BUILDING_ID = 100;

        // 分流器 ID
        public const int ID_SPLITTER = 600;
    }
    #endregion
}