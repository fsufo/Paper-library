using UnityEngine;

namespace GeminFactory
{
    /// <summary>
    /// 传送带主题配置 (ScriptableObject)
    /// 定义传送带在不同连接状态下使用的 Prefab。
    /// </summary>
    [CreateAssetMenu(fileName = "NewBeltTheme", menuName = "Factory/Belt Theme")]
    public class BeltThemeSO : ScriptableObject
    {
        [Header("Basic")]
        public GameObject beltPrefab;       // 直行 (默认)

        [Header("Turns")]
        public GameObject beltTurnLeftPrefab;
        public GameObject beltTurnRightPrefab;

        [Header("Intersections (T-Junctions)")]
        public GameObject beltTLeftPrefab;
        public GameObject beltTRightPrefab;
        public GameObject beltTMergePrefab;

        [Header("Intersections (Cross)")]
        public GameObject beltCrossPrefab;

        [Header("Ends / Starts")]
        public GameObject beltStartPrefab;
        public GameObject beltEndPrefab;
        public GameObject beltSinglePrefab;

        [Header("End Turns (Optional)")]
        public GameObject beltEndTurnLeftPrefab;
        public GameObject beltEndTurnRightPrefab;
        
        [Header("End Intersections (Optional)")]
        public GameObject beltEndMergePrefab;
        public GameObject beltEndTLeftPrefab;
        public GameObject beltEndTRightPrefab;
        public GameObject beltEndCrossPrefab;
    }
}