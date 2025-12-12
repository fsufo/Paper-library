using UnityEngine;

namespace GeminFactory.Data
{
    /// <summary>
    /// 物品定义
    /// </summary>
    [CreateAssetMenu(fileName = "NewItem", menuName = "Factory/Item")]
    public class ItemSO : ScriptableObject
    {
        public string itemName;
        public Sprite icon;
        public Color color = Color.white; // 对应 Compute Shader 里的颜色
        public int price = 10; // 物品售价
        
        [SerializeField, ReadOnly] 
        private int _id;
        public int id => _id;

        private void OnValidate()
        {
            if (_id == 0)
            {
                _id = System.Guid.NewGuid().GetHashCode();
                // 确保 ID 不为 0 (0 通常保留为空)
                if (_id == 0) _id = 1; 
            }
            
            if (string.IsNullOrEmpty(itemName))
            {
                itemName = name;
            }
        }
    }

    // 简单的 ReadOnly 属性绘制器 (为了不引入 Editor 文件夹的复杂性，这里仅作为标记，
    // 实际在 Inspector 中变灰需要 Editor 脚本。
    // 如果不想写 Editor 脚本，可以直接用 [HideInInspector] 但那样就看不到了)
    public class ReadOnlyAttribute : PropertyAttribute { }
}