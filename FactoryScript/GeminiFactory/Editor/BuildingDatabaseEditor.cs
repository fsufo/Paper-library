using UnityEngine;
using UnityEditor;
using GeminFactory.Data;
using System.Collections.Generic;

namespace GeminFactory.Editor
{
    [CustomEditor(typeof(BuildingDatabaseSO))]
    public class BuildingDatabaseEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            BuildingDatabaseSO database = (BuildingDatabaseSO)target;

            GUILayout.Space(20);
            if (GUILayout.Button("Auto Populate Menu Structure", GUILayout.Height(40)))
            {
                PopulateDatabase(database);
            }
        }

        private void PopulateDatabase(BuildingDatabaseSO database)
        {
            Undo.RecordObject(database, "Populate Building Database");

            database.rootNodes.Clear();

            // 1. 创建分类节点
            var productionNode = CreateCategoryNode("Production");
            var logisticsNode = CreateCategoryNode("Logistics");
            var powerNode = CreateCategoryNode("Power");
            var otherNode = CreateCategoryNode("Other");
            var toolsNode = CreateCategoryNode("Tools");

            // 2. 添加基础工具
            // Belt -> Logistics
            logisticsNode.children.Add(new MenuNodeDefinition 
            { 
                name = "Conveyor Belt", 
                type = MenuItemType.Tool, 
                toolMode = BuildMode.Belt 
            });

            // Delete -> Tools
            toolsNode.children.Add(new MenuNodeDefinition 
            { 
                name = "Demolish", 
                type = MenuItemType.Tool, 
                toolMode = BuildMode.Delete 
            });

            // 3. 扫描所有 BuildingDataSO
            string[] guids = AssetDatabase.FindAssets("t:BuildingDataSO");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                BuildingDataSO building = AssetDatabase.LoadAssetAtPath<BuildingDataSO>(path);

                if (building == null) continue;

                MenuNodeDefinition node = new MenuNodeDefinition
                {
                    name = building.name,
                    icon = building.icon,
                    type = MenuItemType.Building,
                    buildingData = building
                };

                switch (building.category)
                {
                    case BuildingCategory.Production:
                        productionNode.children.Add(node);
                        break;
                    case BuildingCategory.Logistics:
                        logisticsNode.children.Add(node);
                        break;
                    case BuildingCategory.Power:
                        powerNode.children.Add(node);
                        break;
                    case BuildingCategory.Decoration:
                        otherNode.children.Add(node);
                        break;
                    default:
                        otherNode.children.Add(node);
                        break;
                }
            }

            // 4. 将非空分类添加到根节点
            if (productionNode.children.Count > 0) database.rootNodes.Add(productionNode);
            if (logisticsNode.children.Count > 0) database.rootNodes.Add(logisticsNode);
            if (powerNode.children.Count > 0) database.rootNodes.Add(powerNode);
            if (otherNode.children.Count > 0) database.rootNodes.Add(otherNode);
            if (toolsNode.children.Count > 0) database.rootNodes.Add(toolsNode);

            EditorUtility.SetDirty(database);
            Debug.Log("Building Database populated successfully!");
        }

        private MenuNodeDefinition CreateCategoryNode(string name)
        {
            return new MenuNodeDefinition
            {
                name = name,
                type = MenuItemType.Category,
                children = new List<MenuNodeDefinition>()
            };
        }
    }
}
