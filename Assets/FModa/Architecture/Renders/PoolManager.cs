using System.Collections.Generic;
using UnityEngine.Pool;
using UnityEngine;

namespace FModa.Pool
{
    public class PoolManager
    {
        private Dictionary<GameObject, ObjectPool<GameObject>> poolDictionary = new Dictionary<GameObject, ObjectPool<GameObject>>();

        /// <summary>
        /// 创建对象池
        /// </summary>
        /// <param name="poolable"></param>
        public void CreatePool(Poolable poolable)
        {
            //创建对象池
            var pool = new ObjectPool<GameObject>(poolable.CreateFunc,
             poolable.ActionOnGet,
              poolable.ActionOnRelease,
               poolable.ActionOnDestroy,
                poolable.collectionCheck,
                 poolable.defaultCapacity,
                  poolable.maxSize);

            //将对象池放入字典
            poolDictionary.Add(poolable.gameObject, pool);

        }

        /// <summary>
        /// 获取对象
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public GameObject GetObject(GameObject source)
        {
            if (!poolDictionary.ContainsKey(source))
            {
                source.TryGetComponent<Poolable>(out var poolable);

                if (poolable == null)
                {
                    Debug.LogError("无法为没有Poolable组件的对象创建对象池");
                    return null;
                }
                else
                {
                    CreatePool(poolable);
                }
            }

            return poolDictionary[source].Get();
        }

        /// <summary>
        /// 回收对象
        /// </summary>
        /// <param name="source"></param>
        /// <param name="go"></param>
        public void ReleaseObject(GameObject source, GameObject go)
        {
            if (!poolDictionary.ContainsKey(source))
            {
                Debug.LogError($"{source.name}对象池不存在!无法回收");
                return;
            }

            poolDictionary[source].Release(go);
        }
    }
}

