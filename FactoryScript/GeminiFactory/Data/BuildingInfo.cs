using UnityEngine;

namespace GeminFactory
{
    /// <summary>
    /// 建筑运行时信息
    /// 挂载在建筑 GameObject 上，用于存储该建筑的配置数据引用。
    /// </summary>
    public class BuildingInfo : MonoBehaviour
    {
        public BuildingDataSO data;
        public Vector2Int origin; // 记录建筑的原点坐标 (左下角)
    }
}