using UnityEngine;

namespace GeminFactory
{
    /// <summary>
    /// 游戏流程控制系统
    /// <para>负责管理游戏的宏观状态 (Running, Loading, WarmUp, Paused)。</para>
    /// <para>根据当前状态，决定每一帧分发哪些更新事件。</para>
    /// </summary>
    public class GameFlowSystem
    {
        public enum GameState
        {
            Running,    // 正常运行：输入 + 物理 + 逻辑 + 渲染
            Loading,    // 加载中：仅渲染 (或显示加载界面)
            WarmUp,     // 预热中：物理 + 渲染 (等待 GPU 数据同步)
            Paused      // 暂停：仅渲染 (可处理 UI 输入)
        }

        private GameState currentState = GameState.Running;
        private int warmUpTimer = 0;
        private const int WARM_UP_DURATION = 5; // 预热帧数

        public void Initialize(GameContext context)
        {
            // 监听加载事件
            GameEventBus.OnLoadRequest += OnLoadRequest;
            GameEventBus.OnLoadComplete += OnLoadComplete;
        }

        public void Dispose()
        {
            GameEventBus.OnLoadRequest -= OnLoadRequest;
            GameEventBus.OnLoadComplete -= OnLoadComplete;
        }

        private void OnLoadRequest(string filename)
        {
            currentState = GameState.Loading;
            Debug.Log("GameFlow: State changed to LOADING");
        }

        private void OnLoadComplete()
        {
            currentState = GameState.WarmUp;
            warmUpTimer = WARM_UP_DURATION;
            Debug.Log($"GameFlow: State changed to WARM_UP ({warmUpTimer} frames)");
        }

        /// <summary>
        /// 每帧调用 (由 FactoryGameManager 驱动)
        /// </summary>
        public void Tick()
        {
            // 1. 始终处理渲染
            GameEventBus.PublishRenderUpdate();

            // 2. 根据状态分发其他事件
            switch (currentState)
            {
                case GameState.Running:
                    GameEventBus.PublishInputUpdate();
                    GameEventBus.PublishPhysicsUpdate(); // TransportSystem
                    GameEventBus.PublishLogicUpdate();   // ProductionSystem
                    break;

                case GameState.WarmUp:
                    // 预热阶段：只运行物理系统，让物品位置和占用数据在 GPU 上就位
                    // 不运行输入和生产逻辑
                    GameEventBus.PublishPhysicsUpdate();
                    
                    warmUpTimer--;
                    if (warmUpTimer <= 0)
                    {
                        currentState = GameState.Running;
                        Debug.Log("GameFlow: WarmUp complete. State changed to RUNNING");
                    }
                    break;

                case GameState.Loading:
                    // 加载中，什么都不做 (除了渲染)
                    break;

                case GameState.Paused:
                    // 暂停中，可能只允许 UI 输入 (此处暂不处理)
                    break;
            }
        }
    }
}
