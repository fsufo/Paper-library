using System;
using UnityEngine;
using GeminFactory.UI;

namespace GeminFactory
{
    /// <summary>
    /// 游戏事件总线
    /// 提供全局的事件发布和订阅功能，用于解耦各个系统。
    /// </summary>
    public static class GameEventBus
    {
        #region Input Events (User Actions)
        /// <summary>请求在指定位置建造建筑</summary>
        public static event Action<Vector2Int, BuildingDataSO> OnBuildRequest;
        /// <summary>请求建造传送带路径 (Start, End, IsAlternatePath)</summary>
        // [Modified] Support Vector3Int for Height
        public static event Action<Vector3Int, Vector3Int, bool> OnBeltBuildRequest;
        /// <summary>请求删除指定位置的对象</summary>
        public static event Action<Vector2Int> OnDeleteRequest;
        /// <summary>请求检查指定位置的对象信息</summary>
        public static event Action<Vector2Int> OnInspectRequest;
        /// <summary>取消当前的检查操作</summary>
        public static event Action OnInspectCancel;
        /// <summary>请求保存游戏 (文件名)</summary>
        public static event Action<string> OnSaveRequest;
        /// <summary>请求加载游戏 (文件名)</summary>
        public static event Action<string> OnLoadRequest;
        /// <summary>加载完成 (通知系统恢复运行)</summary>
        public static event Action OnLoadComplete;
        #endregion

        #region Gameplay Events (Logic Feedback)
        /// <summary>建筑已成功放置</summary>
        public static event Action<Vector2Int, BuildingDataSO> OnBuildingPlaced;
        /// <summary>对象已成功删除</summary>
        public static event Action<Vector2Int> OnObjectDeleted;
        #endregion
        
        #region System Notification Events (Data Sync)
        /// <summary>指定位置的传送带状态发生改变 (需要更新视觉)</summary>
        public static event Action<Vector2Int> OnBeltChanged; 
        /// <summary>地图数据发生改变 (需要同步到 GPU)</summary>
        public static event Action OnMapDataChanged;          
        /// <summary>金钱数量更新 (总量)</summary>
        public static event Action<int> OnMoneyUpdated;       
        /// <summary>获得金钱 (增量)</summary>
        public static event Action<int> OnMoneyEarned;
        #endregion
        
        #region UI Events
        /// <summary>环形菜单项被选择</summary>
        public static event Action<RadialMenuItem> OnMenuItemSelected; 
        #endregion

        #region Lifecycle Events
        /// <summary>游戏核心系统初始化完成</summary>
        public static event Action<GameContext> OnGameInitialized; 
        #endregion

        #region Update Loop Events
        /// <summary>每帧输入更新</summary>
        public static event Action OnInputUpdate;
        
        /// <summary>每帧物理模拟更新 (TransportSystem)</summary>
        public static event Action OnPhysicsUpdate;
        
        /// <summary>每帧游戏逻辑更新 (ProductionSystem)</summary>
        public static event Action OnLogicUpdate;
        
        /// <summary>每帧渲染更新</summary>
        public static event Action OnRenderUpdate;
        
        /// <summary>请求回读统计数据</summary>
        public static event Action OnRequestStatsReadback;
        #endregion

        #region Methods to Publish Events
        
        public static void RequestBuild(Vector2Int pos, BuildingDataSO data) => OnBuildRequest?.Invoke(pos, data);
        // [Modified] Support Vector3Int
        public static void RequestBeltBuild(Vector3Int start, Vector3Int end, bool altPath) => OnBeltBuildRequest?.Invoke(start, end, altPath);
        public static void RequestDelete(Vector2Int pos) => OnDeleteRequest?.Invoke(pos);
        public static void RequestInspect(Vector2Int pos) => OnInspectRequest?.Invoke(pos);
        public static void CancelInspect() => OnInspectCancel?.Invoke();
        public static void RequestSave(string filename) => OnSaveRequest?.Invoke(filename);
        public static void RequestLoad(string filename) => OnLoadRequest?.Invoke(filename);
        public static void PublishLoadComplete() => OnLoadComplete?.Invoke();

        public static void PublishBuildingPlaced(Vector2Int pos, BuildingDataSO data) => OnBuildingPlaced?.Invoke(pos, data);
        public static void PublishObjectDeleted(Vector2Int pos) => OnObjectDeleted?.Invoke(pos);
        
        public static void PublishBeltChanged(Vector2Int pos) => OnBeltChanged?.Invoke(pos);
        public static void PublishMapDataChanged() => OnMapDataChanged?.Invoke();
        public static void PublishMoneyUpdated(int amount) => OnMoneyUpdated?.Invoke(amount);
        public static void PublishMoneyEarned(int amount) => OnMoneyEarned?.Invoke(amount);
        
        public static void PublishMenuItemSelected(RadialMenuItem item) => OnMenuItemSelected?.Invoke(item);
        
        public static void PublishGameInitialized(GameContext context) => OnGameInitialized?.Invoke(context);

        public static void PublishInputUpdate() => OnInputUpdate?.Invoke();
        
        public static void PublishPhysicsUpdate() => OnPhysicsUpdate?.Invoke();
        public static void PublishLogicUpdate() => OnLogicUpdate?.Invoke();
        
        public static void PublishRenderUpdate() => OnRenderUpdate?.Invoke();
        public static void PublishRequestStatsReadback() => OnRequestStatsReadback?.Invoke();
        #endregion
    }
}