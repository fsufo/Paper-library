using UnityEngine;
using System.Collections.Generic;
using GeminFactory; // 引用 BuildMode 等定义

namespace GeminFactory.UI
{
    [System.Serializable]
    public class RadialMenuItem
    {
        public string name;
        public Sprite icon;
        
        [Header("Action")]
        [Tooltip("选择此项时的行为模式")]
        public BuildMode buildMode = BuildMode.None;
        [Tooltip("如果是 Factory/Shop 模式，必须在此处拖入对应的 BuildingDataSO")]
        public BuildingDataSO buildingData; 
        
        [Header("Hierarchy")]
        [Tooltip("是否自动从 BuildingDatabase 填充子节点")]
        public bool autoFillFromDatabase;
        public List<RadialMenuItem> children = new List<RadialMenuItem>();
    }

    [CreateAssetMenu(fileName = "RadialMenuConfig", menuName = "Factory/Radial Menu Config")]
    public class RadialMenuDataSO : ScriptableObject
    {
        [TextArea(5, 15)]
        [SerializeField] private string _usageGuide = 
            "【注意】\n" +
            "菜单结构现在由 BuildingDatabaseSO 定义。\n" +
            "此文件仅用于配置视觉样式 (颜色、半径等)。\n" +
            "下方的 Items 列表已被废弃，不会生效。";

        [HideInInspector] // 隐藏废弃字段
        public List<RadialMenuItem> items = new List<RadialMenuItem>();
        
        [Header("Visual Settings")]
        public float innerRadius = 100f;
        public float outerRadius = 200f;
        public float iconSize = 40f;
        public Color normalColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        public Color highlightColor = new Color(1f, 0.8f, 0.2f, 1f);
        public float lineWidth = 2f;
    }
}