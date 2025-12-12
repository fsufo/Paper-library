using UnityEngine;
using System.Collections.Generic;

using GeminFactory.Data;

namespace GeminFactory
{
    /// <summary>
    /// 建筑分类
    /// </summary>
    public enum BuildingCategory
    {
        Production, // 生产类 (矿机, 加工厂)
        Power,      // 电力类 (发电机, 电线杆)
        Logistics,  // 物流类 (传送带, 分流器)
        Decoration  // 装饰类
    }

    /// <summary>
    /// 端口数据结构
    /// 定义建筑输入/输出口的位置和方向。
    /// </summary>
    [System.Serializable]
    public struct PortData
    {
        public Vector2Int position; // 相对坐标 (0,0) 到 (width-1, height-1)
        public int direction;       // 1=Up, 2=Down, 3=Left, 4=Right
    }

    /// <summary>
    /// 建筑配置数据 (ScriptableObject)
    /// 定义建筑的 Prefab、尺寸、类型和端口信息。
    /// </summary>
    [CreateAssetMenu(fileName = "NewBuildingData", menuName = "Factory/Building Data")]
    public class BuildingDataSO : ScriptableObject
    {
        [Header("Visuals")]
        public GameObject prefab;
        public Sprite icon; // UI 图标
        
        [Header("Dimensions")]
        public int width = 1;
        public int height = 1;
        
        [Header("Type")]
        public BuildingType buildingType;
        public BuildingCategory category;
        
        [Header("Economy")]
        public int price = 100; // 建造价格
        [Tooltip("是否自动出售进入输入口的物品 (用于商店)")]
        public bool autoSellItems; 

        [Header("Production Settings")]
        [Tooltip("生产配方 (矿机或加工厂)")]
        public RecipeSO recipe; 
        [Tooltip("最大原料库存容量")]
        public int maxInventorySize = 20;
        
        [Header("Power Settings")]
        public bool requiresPower;      // 是否耗电
        public float powerConsumption;  // 耗电量 (MW)
        
        public bool generatesPower;     // 是否发电
        public float powerGeneration;   // 发电量 (MW)
        [Tooltip("供电/输电半径 (格)")]
        public float powerRadius;       // 供电/输电半径

        [Header("Ports")]
        public List<PortData> inputs = new List<PortData>();
        public List<PortData> outputs = new List<PortData>();
    }
}