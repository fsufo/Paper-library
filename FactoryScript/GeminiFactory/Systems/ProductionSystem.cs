using UnityEngine;
using System.Collections.Generic;
using GeminFactory.Data;

namespace GeminFactory
{
    /// <summary>
    /// 生产系统 (逻辑层)
    /// 负责处理工厂的输入、加工、产出逻辑，以及库存管理。
    /// </summary>
    public class ProductionSystem
    {
        #region Internal State
        private MapManager mapManager;
        private TransportSystem transportSystem;
        
        // Key: Factory Origin Position
        private Dictionary<Vector2Int, FactoryState> factoryStates = new Dictionary<Vector2Int, FactoryState>();
        
        // 维护所有工厂的列表 (从 MapManager 移过来)
        private List<Vector2Int> factories = new List<Vector2Int>();
        
        private int currentMoney = 0;
        #endregion

        #region Initialization
        public void Initialize(GameContext context)
        {
            mapManager = context.MapManager;
            transportSystem = context.TransportSystem; // 需要在 Context 中添加 TransportSystem

            GameEventBus.OnSimulationUpdate += ProcessFactories;
            
            // 订阅建造和删除事件来维护工厂列表
            GameEventBus.OnBuildingPlaced += OnBuildingPlaced;
            GameEventBus.OnObjectDeleted += OnObjectDeleted;
            
            // 订阅金钱增量事件 (来自 GPU 回读的拆除退款)
            GameEventBus.OnMoneyEarned += OnMoneyEarned;
        }

        public void Dispose()
        {
            GameEventBus.OnSimulationUpdate -= ProcessFactories;
            GameEventBus.OnBuildingPlaced -= OnBuildingPlaced;
            GameEventBus.OnObjectDeleted -= OnObjectDeleted;
            GameEventBus.OnMoneyEarned -= OnMoneyEarned;
        }
        #endregion

        #region Event Handlers
        void OnMoneyEarned(int amount)
        {
            currentMoney += amount;
            GameEventBus.PublishMoneyUpdated(currentMoney);
        }

        void OnBuildingPlaced(Vector2Int pos, BuildingDataSO data)
        {
            // 如果是生产型建筑或商店，加入列表
            if (data.buildingType != BuildingType.Storage) // Storage 暂时不算
            {
                // Miner, Processor, Shop 都需要 Update
                if (data.buildingType == BuildingType.Miner || 
                    data.buildingType == BuildingType.Processor ||
                    data.buildingType == BuildingType.Shop)
                {
                    if (!factories.Contains(pos))
                    {
                        factories.Add(pos);
                    }
                }
            }
        }

        void OnObjectDeleted(Vector2Int pos)
        {
            // 尝试移除。注意：OnObjectDeleted 传来的 pos 可能是建筑的一部分，不一定是原点。
            // 但 BuildingSystem 在删除时，是针对原点发布事件吗？
            // 我们需要检查 BuildingSystem 的实现。
            // 如果 BuildingSystem 发布的 pos 是原点，那就没问题。
            // 如果不是，我们需要通过 MapManager 查找原点。
            // 但此时对象可能已经从 MapManager 移除了...
            
            // 让我们先检查 BuildingSystem 的实现。
            if (factories.Contains(pos))
            {
                factories.Remove(pos);
                factoryStates.Remove(pos);
            }
        }
        #endregion

        #region Logic Loop
        public void ProcessFactories()
        {
            // 1. 遍历所有工厂
            foreach (var factoryPos in factories)
            {
                int index = mapManager.GetIndex(factoryPos);
                if (!mapManager.worldObjects.TryGetValue(index, out GameObject obj)) continue;
                
                var info = obj.GetComponent<GeminFactory.BuildingInfo>();
                if (info == null || info.data == null) continue;

                // 获取或创建状态
                if (!factoryStates.TryGetValue(factoryPos, out FactoryState state))
                {
                    state = new FactoryState();
                    factoryStates[factoryPos] = state;
                }

                // --- A. 输入逻辑 (从输入口吃掉物品) ---
                foreach (var port in info.data.inputs)
                {
                    Vector2Int inputPos = factoryPos + port.position;
                    if (!mapManager.IsValidGridPosition(inputPos)) continue;

                    int idx = mapManager.GetIndex(inputPos);
                    int occupancy = mapManager.gridOccupancyData[idx]; // 0=空, >0=ItemID+1

                    if (occupancy > 0)
                    {
                        bool shouldConsume = false;
                        
                        int itemIndexInGPU = occupancy - 1;
                        int incomingItemId = -1;

                        // 获取物品真实 ID
                        var itemSO = transportSystem.GetItemType(itemIndexInGPU);
                        if (itemSO != null) incomingItemId = itemSO.id;

                        // 1. 检查是否应该消耗物品
                        if (info.data.autoSellItems)
                        {
                            shouldConsume = true; // 商店接收一切
                        }
                        else if (info.data.recipe != null && info.data.recipe.inputs.Count > 0)
                        {
                            // 检查物品是否匹配配方需求
                            bool isRequired = false;
                            foreach(var input in info.data.recipe.inputs)
                            {
                                if (input.item.id == incomingItemId)
                                {
                                    isRequired = true;
                                    break;
                                }
                            }

                            if (isRequired)
                            {
                                // 检查库存是否已满
                                int currentCount = 0;
                                if (state.inventory.TryGetValue(incomingItemId, out int count))
                                {
                                    currentCount = count;
                                }

                                if (currentCount < info.data.maxInventorySize)
                                {
                                    shouldConsume = true;
                                }
                            }
                        }

                        if (shouldConsume)
                        {
                            // 先获取价格 (因为 ConsumeItem 会清除数据)
                            int price = 0;
                            if (info.data.autoSellItems)
                            {
                                price = transportSystem.GetItemPrice(itemIndexInGPU);
                            }

                            // 调用 TransportSystem 销毁物品
                            transportSystem.ConsumeItem(itemIndexInGPU, idx);
                            
                            // 2. 执行消耗后的逻辑
                            if (info.data.autoSellItems)
                            {
                                // 商店逻辑：直接卖钱
                                currentMoney += price;
                                GameEventBus.PublishMoneyUpdated(currentMoney);
                            }
                            else
                            {
                                // 工厂逻辑：增加库存 (使用真实 ID)
                                if (!state.inventory.ContainsKey(incomingItemId)) state.inventory[incomingItemId] = 0;
                                state.inventory[incomingItemId]++;
                            }
                        }
                    }
                }

                // --- B. 加工逻辑 ---
                // 如果没有配方，直接跳过加工逻辑 (如商店)
                if (info.data.recipe == null) continue;

                // 检查是否满足生产条件
                bool canProduce = true;
                
                foreach (var input in info.data.recipe.inputs)
                {
                    if (!state.inventory.ContainsKey(input.item.id) || state.inventory[input.item.id] < input.count)
                    {
                        canProduce = false;
                        break;
                    }
                }

                if (canProduce)
                {
                    if (!state.isProcessing)
                    {
                        foreach (var input in info.data.recipe.inputs)
                        {
                            state.inventory[input.item.id] -= input.count;
                        }
                        state.isProcessing = true;
                        state.progressTimer = 0;
                    }
                }

                // --- C. 进度更新与产出 ---
                if (state.isProcessing)
                {
                    state.progressTimer += Time.deltaTime;
                    
                    if (state.progressTimer >= info.data.recipe.processTime)
                    {
                        // 加工完成，尝试产出
                        bool allOutputSuccess = true;
                        
                        foreach (var output in info.data.recipe.outputs)
                        {
                            bool spawned = false;
                            foreach (var port in info.data.outputs)
                            {
                                // 调用 TransportSystem 生成物品
                                if (transportSystem.TrySpawnItem(factoryPos + port.position, output.item))
                                {
                                    spawned = true;
                                    break;
                                }
                            }
                            
                            if (!spawned) 
                            {
                                allOutputSuccess = false; // 输出堵塞
                                break; 
                            }
                        }

                        if (allOutputSuccess)
                        {
                            state.isProcessing = false;
                            state.progressTimer = 0;
                        }
                        else
                        {
                            state.progressTimer = info.data.recipe.processTime;
                        }
                    }
                }
            }
        }
        #endregion

        #region Public API
        public FactoryState GetFactoryState(Vector2Int pos)
        {
            if (factoryStates.TryGetValue(pos, out FactoryState state))
            {
                return state;
            }
            return null;
        }
        #endregion
    }
}