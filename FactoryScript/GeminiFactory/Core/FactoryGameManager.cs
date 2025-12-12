using UnityEngine;
using UnityEngine.UI;
using GeminFactory.UI;

namespace GeminFactory
{
    /// <summary>
    /// 工厂游戏核心管理器
    /// 负责实例化所有子系统，注入依赖，并在 Update 循环中协调它们的运行。
    /// </summary>
    public class FactoryGameManager : MonoBehaviour
    {
        #region Public Settings (Inspector)

        [Header("Configuration")]
        public GameConfigSO gameConfig;

        [Header("UI References")]
        // public RadialMenu radialMenu; // Removed: Decoupled via EventBus

        #endregion

        #region Sub-Systems (Pure C# Classes)

        private MapManager mapManager;
        private ObjectPoolManager poolManager;
        // private SimulationSystem simulationSystem; // Deprecated
        private TransportSystem transportSystem;
        private ProductionSystem productionSystem;
        
        private VisualSystem visualSystem;
        private BuildingSystem buildingSystem;
        private PreviewSystem previewSystem;
        private InputSystem inputSystem;
        private InspectionSystem inspectionSystem;
        
        private Camera mainCamera; // Internal reference

        #endregion

        #region Unity Lifecycle

        void Start()
        {
            InitializeSystems();
        }

        void Update()
        {
            // 1. 处理输入
            GameEventBus.PublishInputUpdate();

            // 2. 运行模拟 (Compute Shader)
            GameEventBus.PublishSimulationUpdate();

            // 3. 渲染
            GameEventBus.PublishRenderUpdate();

            // 4. 调度数据更新
            ScheduleDataUpdates();
        }

        void OnDestroy()
        {
            // simulationSystem?.Dispose();
            transportSystem?.Dispose();
            productionSystem?.Dispose();
            
            buildingSystem?.Dispose();
            visualSystem?.Dispose();
            inputSystem?.Dispose();
            inspectionSystem?.Dispose();
        }

        #endregion

        #region Initialization

        void InitializeSystems()
        {
            // Auto-detect Main Camera
            if (mainCamera == null) mainCamera = Camera.main;
            
            if (gameConfig == null)
            {
                Debug.LogError("GameConfigSO is missing! Please assign it in the Inspector.");
                return;
            }

            // 1. 实例化子系统
            mapManager = new MapManager();
            poolManager = new ObjectPoolManager();
            // simulationSystem = new SimulationSystem();
            transportSystem = new TransportSystem();
            productionSystem = new ProductionSystem();
            
            visualSystem = new VisualSystem();
            buildingSystem = new BuildingSystem();
            previewSystem = new PreviewSystem();
            inputSystem = new InputSystem();
            inspectionSystem = new InspectionSystem();
            
            // Hierarchy
            Transform objectParent = new GameObject("WorldObjects").transform;
            Transform factoryParent = new GameObject("Factories").transform; factoryParent.SetParent(objectParent);
            Transform shopParent = new GameObject("Shops").transform; shopParent.SetParent(objectParent);
            Transform beltParent = new GameObject("Belts").transform; beltParent.SetParent(objectParent);
            Transform previewParent = new GameObject("Preview").transform;
            Transform poolParent = new GameObject("Pool").transform;

            // 2. 构建 Context
            GameContext context = new GameContext
            {
                MainCamera = mainCamera,
                GameConfig = gameConfig,
                
                WorldObjectParent = objectParent,
                FactoryParent = factoryParent,
                ShopParent = shopParent,
                BeltParent = beltParent,
                PreviewParent = previewParent,
                PoolParent = poolParent,

                MapManager = mapManager,
                PoolManager = poolManager,
                // SimulationSystem = simulationSystem,
                TransportSystem = transportSystem,
                ProductionSystem = productionSystem,
                
                VisualSystem = visualSystem,
                BuildingSystem = buildingSystem,
                PreviewSystem = previewSystem,
                InputSystem = inputSystem,
                InspectionSystem = inspectionSystem
            };

            // 3. 初始化 (注入依赖)
            
            // Map
            mapManager.Initialize(gameConfig.mapWidth, gameConfig.mapHeight);
            
            // Pool
            poolManager.Initialize(poolParent);
            
            // Simulation (Split into Transport and Production)
            transportSystem.Initialize(context);
            productionSystem.Initialize(context);
            
            // Visual
            visualSystem.Initialize(context);
            
            // Building
            buildingSystem.Initialize(context);
            
            // Preview
            previewSystem.Initialize(context);
            
            // Input
            inputSystem.Initialize(context);
            
            // Inspection
            inspectionSystem.Initialize(context);
            
            // UI Initialization
            // if (radialMenu != null && gameConfig.buildingDatabase != null)
            // {
            //     radialMenu.Initialize(gameConfig.buildingDatabase);
            // }

            // 4. 发布初始化完成事件
            GameEventBus.PublishGameInitialized(context);
        }

        #endregion

        #region Updates

        private float statsUpdateTimer = 0;

        void ScheduleDataUpdates()
        {
            // 定期请求全局统计数据更新 (由 TransportSystem 异步处理并发布事件)
            statsUpdateTimer += Time.deltaTime;
            if (statsUpdateTimer < 0.1f) return;
            statsUpdateTimer = 0;

            GameEventBus.PublishRequestStatsReadback();
        }


        #endregion
    }
}