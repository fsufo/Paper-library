using UnityEngine;
using System.Text;
using GeminFactory.Data;

namespace GeminFactory
{
    /// <summary>
    /// 检查系统
    /// 负责查询和汇总游戏对象（建筑、传送带、物品）的详细信息。
    /// </summary>
    public class InspectionSystem
    {
        private MapManager mapManager;
        private ProductionSystem productionSystem;
        private TransportSystem transportSystem;

        public void Initialize(GameContext context)
        {
            this.mapManager = context.MapManager;
            this.productionSystem = context.ProductionSystem;
            this.transportSystem = context.TransportSystem;
        }

        public void Dispose()
        {
            // No events to unsubscribe
        }

        /// <summary>
        /// 获取指定位置的详细信息字符串
        /// </summary>
        public string GetInspectionInfo(Vector2Int pos)
        {
            if (mapManager == null || !mapManager.IsValidGridPosition(pos)) return "Out of Bounds";

            int index = mapManager.GetIndex(pos);
            StringBuilder sb = new StringBuilder();

            // 1. 尝试获取建筑信息
            if (mapManager.worldObjects.TryGetValue(index, out GameObject obj))
            {
                var buildingInfo = obj.GetComponent<BuildingInfo>();
                if (buildingInfo != null && buildingInfo.data != null)
                {
                    sb.AppendLine($"<b>{buildingInfo.data.name}</b>");
                    sb.AppendLine($"Type: {buildingInfo.data.buildingType}");
                    sb.AppendLine($"Pos: {pos}");

                    // 获取工厂状态
                    if (productionSystem != null)
                    {
                        // 使用建筑的原点坐标来查询状态
                        var state = productionSystem.GetFactoryState(buildingInfo.origin);
                        if (state != null)
                        {
                            // 显示配方
                            if (buildingInfo.data.recipe != null)
                            {
                                sb.AppendLine($"Recipe: {buildingInfo.data.recipe.name}");
                                sb.AppendLine($"Time: {state.progressTimer:F1}/{buildingInfo.data.recipe.processTime:F1}s");
                                sb.Append(state.isProcessing ? "(Processing)" : "(Idle)");
                                sb.AppendLine();
                            }

                            // 显示库存
                            if (state.inventory.Count > 0)
                            {
                                sb.AppendLine("Inventory:");
                                foreach (var kvp in state.inventory)
                                {
                                    string itemName = $"Item {kvp.Key}";
                                    // 尝试从配方中查找物品名称
                                    if (buildingInfo.data.recipe != null)
                                    {
                                        foreach(var input in buildingInfo.data.recipe.inputs)
                                            if (input.item.id == kvp.Key) itemName = input.item.itemName;
                                        foreach(var output in buildingInfo.data.recipe.outputs)
                                            if (output.item.id == kvp.Key) itemName = output.item.itemName;
                                    }
                                    
                                    // 显示当前数量 / 最大容量
                                    sb.AppendLine($"  {itemName}: {kvp.Value} / {buildingInfo.data.maxInventorySize}");
                                }
                            }
                            else
                            {
                                sb.AppendLine("Inventory: Empty");
                            }
                        }
                    }

                    // 检查输入口堵塞
                    foreach (var port in buildingInfo.data.inputs)
                    {
                        Vector2Int inputPos = buildingInfo.origin + port.position;
                        if (mapManager.IsValidGridPosition(inputPos))
                        {
                            int idx = mapManager.GetIndex(inputPos);
                            int occupancy = mapManager.gridOccupancyData[idx];
                            if (occupancy > 0)
                            {
                                int itemIndex = occupancy - 1;
                                var itemSO = transportSystem.GetItemType(itemIndex);
                                if (itemSO != null)
                                {
                                    // 检查是否匹配配方
                                    bool isMatch = false;
                                    if (buildingInfo.data.recipe != null)
                                    {
                                        foreach(var input in buildingInfo.data.recipe.inputs)
                                            if (input.item.id == itemSO.id) isMatch = true;
                                    }
                                    else if (buildingInfo.data.autoSellItems)
                                    {
                                        isMatch = true;
                                    }

                                    if (!isMatch)
                                    {
                                        sb.AppendLine($"<color=red>BLOCKED: {itemSO.itemName} (Invalid)</color>");
                                    }
                                }
                            }
                        }
                    }

                    return sb.ToString();
                }
            }
            
            // 2. 尝试获取传送带信息
            int mapVal = mapManager.mapCells[index].type;
            if (mapVal > 0 && mapVal < FactoryConstants.MIN_BUILDING_ID)
            {
                sb.AppendLine("<b>Conveyor Belt</b>");
                sb.AppendLine($"Dir: {mapVal}");
                sb.AppendLine($"Pos: {pos}");

                int occupancy = mapManager.gridOccupancyData[index];
                if (occupancy > 0) 
                {
                    int itemIndex = occupancy - 1;
                    string itemName = "Unknown Item";
                    if (transportSystem != null)
                    {
                        var itemSO = transportSystem.GetItemType(itemIndex);
                        if (itemSO != null) itemName = itemSO.itemName;
                    }
                    
                    sb.AppendLine("Status: Transporting");
                    sb.AppendLine($"Item: {itemName}");
                }
                else
                {
                    sb.AppendLine("Status: Empty");
                }
                return sb.ToString();
            }
            
            return "Empty Grid";
        }
    }
}