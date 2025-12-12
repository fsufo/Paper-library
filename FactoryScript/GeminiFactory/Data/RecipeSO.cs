using UnityEngine;
using System.Collections.Generic;

namespace GeminFactory.Data
{
    [System.Serializable]
    public struct ItemCount
    {
        public ItemSO item;
        public int count;
    }

    /// <summary>
    /// 加工配方
    /// </summary>
    [CreateAssetMenu(fileName = "NewRecipe", menuName = "Factory/Recipe")]
    public class RecipeSO : ScriptableObject
    {
        [Header("Inputs (原料)")]
        [Tooltip("如果是矿机，这里留空")]
        public List<ItemCount> inputs = new List<ItemCount>();

        [Header("Outputs (产物)")]
        public List<ItemCount> outputs = new List<ItemCount>();

        [Header("Settings")]
        [Tooltip("加工所需时间 (秒)")]
        public float processTime = 1.0f;
    }
}
