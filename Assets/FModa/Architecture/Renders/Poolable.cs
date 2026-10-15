using UnityEngine;

namespace FModa.Pool
{
    /// <summary>
    /// 对象池类，任何继承这个类的对象都可以被对象池管理
    /// </summary>
    public abstract class Poolable : MonoBehaviour
    {
        /// <summary>
        /// 用于从对象池中获取对象
        /// </summary>
        [HideInInspector] public GameObject source;

        /// <summary>
        /// 安全检查，当有对象被重复回收时抛出异常
        /// </summary>
        public bool collectionCheck = true;

        /// <summary>
        /// 该对象常态情况下的留存的数量
        /// </summary>
        public int defaultCapacity = 0;

        /// <summary>
        /// 游戏中予许同时存在的最大数量
        /// </summary>
        public int maxSize = 100;

        /// <summary>
        /// 对象池无可用对象时调用
        /// </summary>
        /// <returns></returns>
        public virtual GameObject CreateFunc()
        {
            GameObject go = GameObject.Instantiate(gameObject);
            go.GetComponent<Poolable>().source = gameObject;
            return go;
        }

        /// <summary>
        /// 从对象池中获取对象时调用
        /// </summary>
        /// <param name="go"></param>
        public virtual void ActionOnGet(GameObject go)
        {
            go.SetActive(true);
        }

        /// <summary>
        /// 对象池回收对象时调用
        /// </summary>
        /// <param name="go"></param>
        public virtual void ActionOnRelease(GameObject go)
        {
            go.SetActive(false);
        }

        /// <summary>
        /// 对象池销毁对象时调用
        /// </summary>
        /// <param name="go"></param>
        public virtual void ActionOnDestroy(GameObject go)
        {
            Destroy(go);
        }
    }

}
