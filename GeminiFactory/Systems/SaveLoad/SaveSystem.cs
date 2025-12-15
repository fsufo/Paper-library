using UnityEngine;
using System.Collections.Generic;
using System.IO;
using GeminFactory.Data;

namespace GeminFactory.Systems.SaveLoad
{
    public class SaveSystem
    {
        private GameContext context;
        private BuildingDatabaseSO buildingDatabase;
        private Dictionary<int, ItemSO> itemCache = new Dictionary<int, ItemSO>();
        private Dictionary<string, BuildingDataSO> buildingDataCache = new Dictionary<string, BuildingDataSO>();

        public void Initialize(GameContext context)
        {
            this.context = context;
            this.buildingDatabase = context.GameConfig.buildingDatabase;
            
            BuildItemCache();
            BuildBuildingDataCache();

            GameEventBus.OnSaveRequest += SaveToFile;
            GameEventBus.OnLoadRequest += LoadFromFile;
        }

        private void BuildItemCache()
        {
            itemCache.Clear();
            if (buildingDatabase == null) return;
            
            CollectItemsRecursive(buildingDatabase.rootNodes);
        }

        private void CollectItemsRecursive(List<MenuNodeDefinition> nodes)
        {
            foreach (var node in nodes)
            {
                if (node.type == MenuItemType.Building && node.buildingData != null)
                {
                    var data = node.buildingData;
                    if (data.recipe != null)
                    {
                        foreach (var input in data.recipe.inputs)
                        {
                            if (input.item != null && !itemCache.ContainsKey(input.item.id))
                                itemCache.Add(input.item.id, input.item);
                        }
                        foreach (var output in data.recipe.outputs)
                        {
                            if (output.item != null && !itemCache.ContainsKey(output.item.id))
                                itemCache.Add(output.item.id, output.item);
                        }
                    }
                }
                else if (node.type == MenuItemType.Category)
                {
                    CollectItemsRecursive(node.children);
                }
            }
        }

        private void BuildBuildingDataCache()
        {
            buildingDataCache.Clear();
            if (buildingDatabase == null) return;
            CollectBuildingDataRecursive(buildingDatabase.rootNodes);
        }

        private void CollectBuildingDataRecursive(List<MenuNodeDefinition> nodes)
        {
            foreach (var node in nodes)
            {
                if (node.type == MenuItemType.Building && node.buildingData != null)
                {
                    if (!buildingDataCache.ContainsKey(node.buildingData.name))
                    {
                        buildingDataCache.Add(node.buildingData.name, node.buildingData);
                    }
                }
                else if (node.type == MenuItemType.Category)
                {
                    CollectBuildingDataRecursive(node.children);
                }
            }
        }

        public void Dispose()
        {
            GameEventBus.OnSaveRequest -= SaveToFile;
            GameEventBus.OnLoadRequest -= LoadFromFile;
        }

        #region Save
        public void SaveToFile(string filename)
        {
            SaveData data = CaptureSaveData();
            string json = JsonUtility.ToJson(data, true);
            
            // 保存到 Assets/Scripts/GeminiFactory/Assets/Save 目录
            // 注意：在构建后的游戏中，Application.dataPath 指向 Data 文件夹，无法写入 Assets
            // 这里仅用于编辑器环境
#if UNITY_EDITOR
            string folder = Path.Combine(Application.dataPath, "Scripts/GeminiFactory/Assets/Save");
#else
            string folder = Application.persistentDataPath;
#endif
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            
            string path = Path.Combine(folder, filename + ".json");
            File.WriteAllText(path, json);
            Debug.Log($"Game saved to {path}");
            
#if UNITY_EDITOR
            UnityEditor.AssetDatabase.Refresh();
#endif
        }

        public SaveData CaptureSaveData()
        {
            SaveData data = new SaveData();
            var mapManager = context.MapManager;
            var productionSystem = context.ProductionSystem;
            var transportSystem = context.TransportSystem;

            data.width = mapManager.mapWidth;
            data.height = mapManager.mapHeight;
            
            // 0. Save Money
            data.money = productionSystem.GetCurrentMoney();

            // 1. Save Belts
            // Iterate all cells to find belts (type 1-4)
            // Optimization: Iterate beltObjects dictionary instead
            foreach (var kvp in mapManager.beltObjects)
            {
                int index = kvp.Key;
                Vector3Int pos = mapManager.GetPos3DFromIndex(index); // Use 3D Pos
                int type = mapManager.mapCells[index].type;
                
                // 支持普通传送带 (1-4) 和电梯 (700+)
                if ((type >= 1 && type <= 4) || type >= FactoryConstants.ID_ELEVATOR_UP_BASE)
                {
                    data.belts.Add(new SavedBelt
                    {
                        x = pos.x,
                        y = pos.y,
                        direction = type,
                        height = pos.z // Save Layer
                    });
                }
            }

            // 2. Save Buildings
            HashSet<GameObject> processedBuildings = new HashSet<GameObject>();
            foreach (var kvp in mapManager.worldObjects)
            {
                GameObject obj = kvp.Value;
                if (obj == null || processedBuildings.Contains(obj)) continue;

                processedBuildings.Add(obj);
                var info = obj.GetComponent<BuildingInfo>();
                if (info != null && info.data != null)
                {
                    var savedBuilding = new SavedBuilding
                    {
                        x = info.origin.x,
                        y = info.origin.y,
                        buildingName = info.data.name
                    };

                    // Save State
                    var state = productionSystem.GetFactoryState(info.origin);
                    if (state != null)
                    {
                        savedBuilding.progressTimer = state.progressTimer;
                        savedBuilding.isProcessing = state.isProcessing;
                        foreach(var invKvp in state.inventory)
                        {
                            savedBuilding.inventory.Add(new SavedInventoryItem { itemID = invKvp.Key, count = invKvp.Value });
                        }
                    }

                    data.buildings.Add(savedBuilding);
                }
            }

            // 3. Save Items
            data.items = transportSystem.CaptureAllItems();

            return data;
        }
        #endregion

        #region Load
        public void LoadFromFile(string filename)
        {
#if UNITY_EDITOR
            string folder = Path.Combine(Application.dataPath, "Scripts/GeminiFactory/Assets/Save");
#else
            string folder = Application.persistentDataPath;
#endif
            string path = Path.Combine(folder, filename + ".json");
            
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                SaveData data = JsonUtility.FromJson<SaveData>(json);
                LoadSaveData(data);
                Debug.Log($"Game loaded from {path}");
                
                // 通知加载完成
                GameEventBus.PublishLoadComplete();
            }
            else
            {
                Debug.LogError($"Save file not found: {path}");
            }
        }

        public void LoadFromSO(MapLayoutSO layout)
        {
            if (layout != null && layout.data != null)
            {
                LoadSaveData(layout.data);
                // 通知加载完成
                GameEventBus.PublishLoadComplete();
            }
        }

        private void LoadSaveData(SaveData data)
        {
            // 1. Clear existing map
            ClearMap();
            
            // 2. Restore Money
            context.ProductionSystem.SetCurrentMoney(data.money);

            // 3. Load Buildings
            foreach (var savedBuilding in data.buildings)
            {
                BuildingDataSO buildingData = FindBuildingData(savedBuilding.buildingName);
                if (buildingData != null)
                {
                    context.BuildingSystem.PlaceBuilding(new Vector2Int(savedBuilding.x, savedBuilding.y), buildingData);
                    
                    // Restore State
                    FactoryState state = new FactoryState();
                    state.progressTimer = savedBuilding.progressTimer;
                    state.isProcessing = savedBuilding.isProcessing;
                    foreach(var invItem in savedBuilding.inventory)
                    {
                        state.inventory[invItem.itemID] = invItem.count;
                    }
                    context.ProductionSystem.SetFactoryState(new Vector2Int(savedBuilding.x, savedBuilding.y), state);
                }
                else
                {
                    Debug.LogWarning($"Building data not found for: {savedBuilding.buildingName}");
                }
            }

            // 4. Load Belts
            foreach (var savedBelt in data.belts)
            {
                // Pass Vector3Int
                context.BuildingSystem.SetBeltDirection(new Vector3Int(savedBelt.x, savedBelt.y, savedBelt.height), savedBelt.direction);
            }
            
            // 5. Load Items
            context.TransportSystem.RestoreItems(data.items);
            // Restore Item Types
            for(int i=0; i<data.items.Count; i++)
            {
                if (itemCache.TryGetValue(data.items[i].itemID, out ItemSO itemSO))
                {
                    context.TransportSystem.SetItemType(i, itemSO);
                }
            }
        }

        private void ClearMap()
        {
            context.BuildingSystem.ClearAll();
        }

        private BuildingDataSO FindBuildingData(string name)
        {
            if (buildingDataCache.TryGetValue(name, out var data))
            {
                return data;
            }
            
            // Fallback: 如果缓存中没有，尝试递归查找（通常不应该发生）
            return FindBuildingRecursive(buildingDatabase.rootNodes, name);
        }

        private BuildingDataSO FindBuildingRecursive(List<MenuNodeDefinition> nodes, string name)
        {
            foreach (var node in nodes)
            {
                if (node.type == MenuItemType.Building && node.buildingData != null)
                {
                    if (node.buildingData.name == name) return node.buildingData;
                }
                else if (node.type == MenuItemType.Category)
                {
                    var result = FindBuildingRecursive(node.children, name);
                    if (result != null) return result;
                }
            }
            return null;
        }
        #endregion
    }
}