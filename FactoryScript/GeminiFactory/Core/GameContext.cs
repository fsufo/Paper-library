using UnityEngine;
using GeminFactory.UI;

namespace GeminFactory
{
    /// <summary>
    /// 游戏上下文 (Service Locator)
    /// <para>作为一个容器，持有所有核心系统的引用，方便在系统初始化阶段传递依赖。</para>
    /// <para>注意：仅在初始化阶段使用，运行时应尽量避免通过 Context 获取引用，而是通过构造函数或 Initialize 方法缓存所需依赖。</para>
    /// </summary>
    public class GameContext
    {
        #region Core Components
        public Camera MainCamera { get; set; }
        public GameConfigSO GameConfig { get; set; }
        #endregion

        #region Scene Hierarchy Parents
        public Transform WorldObjectParent { get; set; }
        public Transform FactoryParent { get; set; }
        public Transform ShopParent { get; set; }
        public Transform BeltParent { get; set; }
        public Transform PreviewParent { get; set; }
        public Transform PoolParent { get; set; }
        #endregion
        
        #region Data Managers
        public MapManager MapManager { get; set; }
        public ObjectPoolManager PoolManager { get; set; }
        #endregion
        
        #region Systems
        public TransportSystem TransportSystem { get; set; }
        public ProductionSystem ProductionSystem { get; set; }
        
        public VisualSystem VisualSystem { get; set; }
        public BuildingSystem BuildingSystem { get; set; }
        public PreviewSystem PreviewSystem { get; set; }
        public InputSystem InputSystem { get; set; }
        public InspectionSystem InspectionSystem { get; set; }
        #endregion
    }
}