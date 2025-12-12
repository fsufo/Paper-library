using UnityEngine;
using System.Collections.Generic;

namespace GeminFactory
{
    /// <summary>
    /// 对象池管理器
    /// 负责管理 GameObject 的复用，特别是传送带对象。
    /// </summary>
    public class ObjectPoolManager
    {
        #region Fields
        private Transform poolParent;
        
        // Key: Prefab InstanceID, Value: Stack of inactive objects
        private Dictionary<int, Stack<GameObject>> beltPool = new Dictionary<int, Stack<GameObject>>();
        
        // Key: Active Object InstanceID, Value: Source Prefab InstanceID
        // 用于在回收时知道该对象属于哪个池子
        private Dictionary<int, int> activeBeltToPrefabMap = new Dictionary<int, int>();
        #endregion

        #region Initialization
        /// <summary>
        /// 初始化对象池
        /// </summary>
        /// <param name="parent">对象池的父节点，用于存放未激活的对象</param>
        public void Initialize(Transform parent)
        {
            poolParent = parent;
        }
        #endregion

        #region Pool Operations
        /// <summary>
        /// 从池中获取或实例化一个新的传送带对象
        /// </summary>
        public GameObject SpawnBelt(GameObject prefab, Vector3 pos, Quaternion rot)
        {
            if (prefab == null) return null;

            int prefabId = prefab.GetInstanceID();
            if (!beltPool.ContainsKey(prefabId)) beltPool[prefabId] = new Stack<GameObject>();

            GameObject obj;
            if (beltPool[prefabId].Count > 0)
            {
                obj = beltPool[prefabId].Pop();
                obj.transform.position = pos;
                obj.transform.rotation = rot;
                obj.SetActive(true);
            }
            else
            {
                obj = Object.Instantiate(prefab, pos, rot, poolParent);
            }

            // 记录这个物体是哪个 Prefab 生成的，以便回收
            activeBeltToPrefabMap[obj.GetInstanceID()] = prefabId;
            return obj;
        }

        /// <summary>
        /// 回收传送带对象到池中
        /// </summary>
        public void RecycleBelt(GameObject obj)
        {
            if (obj == null) return;

            int objId = obj.GetInstanceID();
            if (activeBeltToPrefabMap.TryGetValue(objId, out int prefabId))
            {
                obj.SetActive(false);
                if (!beltPool.ContainsKey(prefabId)) beltPool[prefabId] = new Stack<GameObject>();
                beltPool[prefabId].Push(obj);
            }
            else
            {
                // 如果不是池子里的物体（比如旧版本生成的），直接销毁
                Object.Destroy(obj);
            }
        }
        #endregion
    }
}