using UnityEngine;

namespace GeminFactory.Systems.SaveLoad
{
    [CreateAssetMenu(fileName = "NewMapLayout", menuName = "Factory/Map Layout")]
    public class MapLayoutSO : ScriptableObject
    {
        public SaveData data;
    }
}
