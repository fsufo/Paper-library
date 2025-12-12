using UnityEngine;

using GeminFactory.Data;

namespace GeminFactory
{
    [CreateAssetMenu(fileName = "GameConfig", menuName = "Factory/Game Config")]
    public class GameConfigSO : ScriptableObject
    {
        [Header("Database")]
        public BuildingDatabaseSO buildingDatabase;

        [Header("Map Settings")]
        public int mapWidth = 256;
        public int mapHeight = 256;

        [Header("Simulation Settings")]
        public int maxItems = 10000;
        public float moveSpeed = 5.0f;
        public float spawnInterval = 0.5f;
        public ComputeShader itemMover;
        public Mesh itemMesh;
        public Material itemMaterial;

        [Header("Game Settings")]
        public float deleteRadius = 2.5f;
        
        [Header("Resources")]
        public BeltThemeSO beltTheme;
        public Material deletePreviewMat;
        
        // 注意：BuildingDataSO 通常是动态获取的，但如果有一些全局默认的，也可以放在这里
        // 目前 InputSystem 依赖 factoryData 和 shopData 作为默认值，我们可以保留在这里
        // 或者让 InputSystem 自己去管理
    }
}