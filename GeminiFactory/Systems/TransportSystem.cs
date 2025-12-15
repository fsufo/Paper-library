using UnityEngine;
using UnityEngine.Rendering;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using GeminFactory.Data;

namespace GeminFactory
{
    /// <summary>
    /// 物流系统 (物理层)
    /// 负责管理 Compute Shader，处理物品的移动、生成、销毁以及 GPU 数据回读。
    /// </summary>
    public class TransportSystem
    {
        #region Settings & Buffers
        private int maxItems;
        private float moveSpeed;
        private ComputeShader itemMover;
        private Mesh itemMesh;

        // --- Buffers ---
        public SimulationBuffers buffers { get; private set; }
        #endregion

        #region Internal State
        private int spawnIndex = 0;
        private bool isOccupancyDataPending = false;
        private bool isStatsDataPending = false;
        private int[] globalStatsData = new int[1];

        // --- Kernels ---
        private int kernelMoveItems;
        private int kernelClearGrid;
        private int kernelMarkGrid;
        private int kernelDeleteItems;

        // --- References ---
        private MapManager mapManager;
        
        // CPU 端维护的物品价格缓存 (Index -> Price)
        private int[] itemPrices;
        // CPU 端维护的物品类型缓存 (Index -> ItemSO)
        private ItemSO[] itemTypes;
        
        // 锁定机制：解决 GPU 回读延迟导致的"幽灵物品"问题
        // Key: GridIndex, Value: (LockedValue, RemainingFrames)
        private Dictionary<int, (int value, int frames)> gridLocks = new Dictionary<int, (int, int)>();
        #endregion

        #region Initialization
        public void Initialize(GameContext context)
        {
            mapManager = context.MapManager;
            var config = context.GameConfig;
            
            this.maxItems = config.maxItems;
            this.moveSpeed = config.moveSpeed;
            this.itemMover = config.itemMover;
            this.itemMesh = config.itemMesh;

            // 初始化价格数组
            itemPrices = new int[maxItems];
            itemTypes = new ItemSO[maxItems];

            // 检查结构体大小一致性
            int stride = Marshal.SizeOf(typeof(ItemData));
            if (stride != 56)
            {
                Debug.LogError($"ItemData struct size mismatch! Expected 56 bytes, got {stride} bytes. Please check C# and HLSL definitions.");
            }

            buffers = new SimulationBuffers();
            buffers.Initialize(maxItems, mapManager.mapCells, itemMesh); // Use mapCells
            
            InitializeKernels();
            BindBuffers();

            GameEventBus.OnMapDataChanged += UpdateMapData;
            GameEventBus.OnPhysicsUpdate += RunSimulation;
            GameEventBus.OnRequestStatsReadback += RequestStatsReadback;
        }

        public void Dispose()
        {
            buffers?.Release();
            GameEventBus.OnMapDataChanged -= UpdateMapData;
            GameEventBus.OnPhysicsUpdate -= RunSimulation;
            GameEventBus.OnRequestStatsReadback -= RequestStatsReadback;
        }

        void InitializeKernels()
        {
            kernelMoveItems = itemMover.FindKernel("CSMain");
            kernelClearGrid = itemMover.FindKernel("ClearGrid");
            kernelMarkGrid = itemMover.FindKernel("MarkGrid");
            kernelDeleteItems = itemMover.FindKernel("DeleteItemsInArea");
        }

        void BindBuffers()
        {
            itemMover.SetBuffer(kernelMoveItems, "Items", buffers.ItemBuffer);
            itemMover.SetBuffer(kernelMoveItems, "Map", buffers.MapBuffer);
            itemMover.SetBuffer(kernelMoveItems, "Grid", buffers.GridOccupancyBuffer);
            itemMover.SetBuffer(kernelMoveItems, "Stats", buffers.GlobalStatsBuffer);
            itemMover.SetInt("Width", mapManager.mapWidth);
            itemMover.SetInt("Height", mapManager.mapHeight);
            itemMover.SetInt("MaxItems", maxItems);

            itemMover.SetBuffer(kernelClearGrid, "Grid", buffers.GridOccupancyBuffer);
            itemMover.SetInt("Width", mapManager.mapWidth);
            itemMover.SetInt("Height", mapManager.mapHeight);

            itemMover.SetBuffer(kernelMarkGrid, "Items", buffers.ItemBuffer);
            itemMover.SetBuffer(kernelMarkGrid, "Grid", buffers.GridOccupancyBuffer);
            itemMover.SetInt("Width", mapManager.mapWidth);
            itemMover.SetInt("Height", mapManager.mapHeight);
            itemMover.SetInt("MaxItems", maxItems);

            itemMover.SetBuffer(kernelDeleteItems, "Items", buffers.ItemBuffer);
            itemMover.SetBuffer(kernelDeleteItems, "Stats", buffers.GlobalStatsBuffer);
        }
        #endregion

        #region Simulation Loop
        public void UpdateMapData()
        {
            buffers.MapBuffer.SetData(mapManager.mapCells); // Use mapCells
        }

        public void RunSimulation()
        {
            // 0. 更新网格锁定状态
            UpdateLocks();

            // [Fix] 限制最大步长为 1/60 秒，防止低帧率下 Shader 内部多次迭代导致的碰撞穿透
            // 这意味着如果渲染帧率低于 60，游戏逻辑速度会变慢，但能保证物理稳定性
            float clampedDeltaTime = Mathf.Min(Time.deltaTime, 1.0f / 60.0f);
            itemMover.SetFloat("DeltaTime", clampedDeltaTime);
            itemMover.SetFloat("MoveSpeed", moveSpeed);

            // 1. 清除网格占用
            int totalCells = mapManager.mapWidth * mapManager.mapHeight * FactoryConstants.MAX_LAYERS;
            int clearGroups = Mathf.CeilToInt(totalCells / 256.0f);
            itemMover.Dispatch(kernelClearGrid, clearGroups, 1, 1);

            // 2. 标记网格占用
            int markGroups = Mathf.CeilToInt(maxItems / 256.0f);
            itemMover.Dispatch(kernelMarkGrid, markGroups, 1, 1);

            // 3. 异步回读
            if (!isOccupancyDataPending)
            {
                AsyncGPUReadback.Request(buffers.GridOccupancyBuffer, OnOccupancyDataReadback);
                isOccupancyDataPending = true;
            }

            // 4. 移动物品
            int moveGroups = Mathf.CeilToInt(maxItems / 64.0f);
            itemMover.Dispatch(kernelMoveItems, moveGroups, 1, 1);
        }

        void OnOccupancyDataReadback(AsyncGPUReadbackRequest request)
        {
            isOccupancyDataPending = false;
            if (request.hasError) return;
            
            var data = request.GetData<int>();
            int[] arr = new int[data.Length];
            data.CopyTo(arr);
            
            // 在传输给 MapManager 之前，先应用本地锁定
            ApplyLocks(arr);
            
            mapManager.UpdateGridOccupancy(arr);
        }
        #endregion

        #region Locking Mechanism
        private void ApplyLocks(int[] data)
        {
            if (gridLocks.Count > 0)
            {
                foreach (var kvp in gridLocks)
                {
                    int idx = kvp.Key;
                    if (idx >= 0 && idx < data.Length)
                    {
                        data[idx] = kvp.Value.value;
                    }
                }
            }
        }

        private void LockGrid(int index, int value, int frames = 5)
        {
            if (index >= 0 && index < mapManager.mapCells.Length)
            {
                gridLocks[index] = (value, frames);
                // 立即更新 MapManager 中的当前数据，以便当帧逻辑能读到最新状态
                // 注意：虽然我们会在下一帧回读时覆盖它，但当前帧的逻辑可能依赖它
                mapManager.gridOccupancyData[index] = value;
            }
        }

        private void UpdateLocks()
        {
            if (gridLocks.Count == 0) return;

            List<int> keysToRemove = new List<int>();
            foreach (var key in new List<int>(gridLocks.Keys))
            {
                var (val, frames) = gridLocks[key];
                frames--;
                if (frames <= 0)
                {
                    gridLocks.Remove(key);
                }
                else
                {
                    gridLocks[key] = (val, frames);
                }
            }
        }
        #endregion

        #region Public API
        /// <summary>
        /// 尝试在指定位置生成物品
        /// </summary>
        public bool TrySpawnItem(Vector2Int pos, ItemSO itemType)
        {
            if (!mapManager.IsValidGridPosition(pos)) return false;
            int idx = mapManager.GetIndex(pos);
            
            // 检查输出口是否被占用 (包含锁定状态)
            if (mapManager.gridOccupancyData[idx] != 0) return false;

            ItemData newItem = new ItemData
            {
                pos = new Vector2(pos.x, pos.y),
                logicPos = new Vector2(pos.x, pos.y), // Initialize logicPos same as pos
                active = 1,
                color = new Vector4(itemType.color.r, itemType.color.g, itemType.color.b, 1.0f),
                price = itemType.price,
                id = itemType.id,
                state = -1,
                height = 0,
                targetHeight = 0
            };

            ItemData[] dataWrapper = new ItemData[] { newItem };
            buffers.ItemBuffer.SetData(dataWrapper, 0, spawnIndex, 1);
            
            // 记录物品价格 (CPU 缓存，用于商店卖出)
            itemPrices[spawnIndex] = itemType.price;
            itemTypes[spawnIndex] = itemType;

            // 锁定网格为占用
            LockGrid(idx, spawnIndex + 1, 5);

            spawnIndex = (spawnIndex + 1) % maxItems;
            
            return true;
        }

        /// <summary>
        /// 销毁指定索引的物品
        /// </summary>
        public void ConsumeItem(int itemIndex, int gridIndex)
        {
            ItemData deadItem = new ItemData { active = 0 };
            ItemData[] wrapper = new ItemData[] { deadItem };
            buffers.ItemBuffer.SetData(wrapper, 0, itemIndex, 1);
            
            // 清除价格 (可选)
            itemPrices[itemIndex] = 0;
            itemTypes[itemIndex] = null;

            // 锁定网格为空
            LockGrid(gridIndex, 0, 5);
        }

        public int GetItemPrice(int itemIndex)
        {
            if (itemIndex >= 0 && itemIndex < maxItems)
            {
                return itemPrices[itemIndex];
            }
            return 0;
        }

        public ItemSO GetItemType(int itemIndex)
        {
            if (itemIndex >= 0 && itemIndex < maxItems)
            {
                return itemTypes[itemIndex];
            }
            return null;
        }

        public void DeleteItemsInArea(Vector3 center, float radius)
        {
            itemMover.SetFloats("DelCenter", center.x, center.z);
            itemMover.SetFloat("DelRadius", radius);
            int deleteGroups = Mathf.CeilToInt(maxItems / 64.0f);
            itemMover.Dispatch(kernelDeleteItems, deleteGroups, 1, 1);
        }

        public void RequestStatsReadback()
        {
            if (!isStatsDataPending)
            {
                AsyncGPUReadback.Request(buffers.GlobalStatsBuffer, (req) => {
                    isStatsDataPending = false;
                    if (req.hasError) return;
                    var data = req.GetData<int>();
                    data.CopyTo(globalStatsData);
                    
                    // 发布增量事件
                    if (globalStatsData[0] > 0)
                    {
                        GameEventBus.PublishMoneyEarned(globalStatsData[0]);
                        
                        // 清零本地缓存和 GPU Buffer
                        globalStatsData[0] = 0;
                        buffers.GlobalStatsBuffer.SetData(globalStatsData);
                    }
                });
                isStatsDataPending = true;
            }
        }
        #endregion

        #region Save/Load Support
        public List<Systems.SaveLoad.SavedItem> CaptureAllItems()
        {
            List<Systems.SaveLoad.SavedItem> savedItems = new List<Systems.SaveLoad.SavedItem>();
            
            // 同步读取 GPU Buffer (可能会导致一帧卡顿，但在保存时是可以接受的)
            ItemData[] currentItems = new ItemData[maxItems];
            buffers.ItemBuffer.GetData(currentItems);

            for (int i = 0; i < maxItems; i++)
            {
                if (currentItems[i].active != 0)
                {
                    savedItems.Add(new Systems.SaveLoad.SavedItem
                    {
                        x = currentItems[i].pos.x,
                        y = currentItems[i].pos.y,
                        itemID = currentItems[i].id,
                        price = currentItems[i].price,
                        r = currentItems[i].color.x,
                        g = currentItems[i].color.y,
                        b = currentItems[i].color.z,
                        height = currentItems[i].targetHeight // Save targetHeight as height
                    });
                }
            }
            return savedItems;
        }

        public void RestoreItems(List<Systems.SaveLoad.SavedItem> savedItems)
        {
            // 1. 重置 Buffer
            ItemData[] newItems = new ItemData[maxItems];
            
            // 2. 填充数据
            int count = Mathf.Min(savedItems.Count, maxItems);
            for (int i = 0; i < count; i++)
            {
                var saved = savedItems[i];
                newItems[i] = new ItemData
                {
                    pos = new Vector2(saved.x, saved.y),
                    logicPos = new Vector2(saved.x, saved.y),
                    active = 1,
                    color = new Vector4(saved.r, saved.g, saved.b, 1.0f),
                    price = saved.price,
                    id = saved.itemID,
                    state = -1,
                    height = saved.height,       // Restore both to saved height
                    targetHeight = saved.height
                };

                // 恢复 CPU 端缓存
                itemPrices[i] = saved.price;
            }

            buffers.ItemBuffer.SetData(newItems);
            spawnIndex = count % maxItems;
        }
        
        public void SetItemType(int index, ItemSO item)
        {
            if (index >= 0 && index < maxItems)
            {
                itemTypes[index] = item;
            }
        }
        #endregion

        #region Buffer Management Class
        public class SimulationBuffers
        {
            public ComputeBuffer ItemBuffer { get; private set; }
            public ComputeBuffer ArgsBuffer { get; private set; }
            public ComputeBuffer MapBuffer { get; private set; }
            public ComputeBuffer GridOccupancyBuffer { get; private set; }
            public ComputeBuffer GlobalStatsBuffer { get; private set; }

            public void Initialize(int maxItems, MapCell[] mapCells, Mesh itemMesh)
            {
                int stride = Marshal.SizeOf(typeof(ItemData));
                ItemBuffer = new ComputeBuffer(maxItems, stride);
                ItemData[] initialItems = new ItemData[maxItems];
                ItemBuffer.SetData(initialItems);

                int mapStride = Marshal.SizeOf(typeof(MapCell)); // 16 bytes
                MapBuffer = new ComputeBuffer(mapCells.Length, mapStride);
                MapBuffer.SetData(mapCells);

                GridOccupancyBuffer = new ComputeBuffer(mapCells.Length, 4);

                uint[] args = new uint[5] { (uint)itemMesh.GetIndexCount(0), (uint)maxItems, 0, 0, 0 };
                ArgsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
                ArgsBuffer.SetData(args);

                GlobalStatsBuffer = new ComputeBuffer(1, 4);
                GlobalStatsBuffer.SetData(new int[1]);
            }

            public void Release()
            {
                ItemBuffer?.Release();
                ArgsBuffer?.Release();
                MapBuffer?.Release();
                GridOccupancyBuffer?.Release();
                GlobalStatsBuffer?.Release();
            }
        }
        #endregion
    }
}