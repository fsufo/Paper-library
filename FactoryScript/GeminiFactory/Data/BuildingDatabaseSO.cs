using UnityEngine;
using System.Collections.Generic;

namespace GeminFactory.Data
{
    public enum MenuItemType
    {
        Category,   // 分类文件夹 (包含子项)
        Building,   // 具体建筑 (对应 BuildingDataSO)
        Tool        // 特殊工具 (Belt, Delete 等)
    }

    [System.Serializable]
    public class MenuNodeDefinition
    {
        public string name;
        public Sprite icon;
        public MenuItemType type;
        
        [Header("Data")]
        [Tooltip("仅当 Type 为 Building 时有效")]
        public BuildingDataSO buildingData;
        
        [Tooltip("仅当 Type 为 Tool 时有效")]
        public BuildMode toolMode; // Belt, Delete
        
        [Header("Hierarchy")]
        [Tooltip("仅当 Type 为 Category 时有效")]
        public List<MenuNodeDefinition> children = new List<MenuNodeDefinition>();
    }

    /// <summary>
    /// 建筑数据库 & 菜单结构定义
    /// <para>定义了环形菜单的层级结构以及所有可用的建筑/工具。</para>
    /// </summary>
    [CreateAssetMenu(fileName = "BuildingDatabase", menuName = "Factory/Building Database")]
    public class BuildingDatabaseSO : ScriptableObject
    {
        [Header("Root Menu Items")]
        public List<MenuNodeDefinition> rootNodes = new List<MenuNodeDefinition>();
    }
}