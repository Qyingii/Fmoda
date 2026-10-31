using System.Collections.Generic;
using UnityEngine;

namespace FModa.Data
{
    /// <summary>
    /// 泛型数据中心
    ///
    /// 数据生命周期事件是「数据中心 → 模块 → 视图」这条反向通知回路的起点：
    /// 任何路径上的增/删/清空都会广播出去，订阅者才能知道自己持有的数据已经没了。
    /// 模块负责把它们翻译成业务语义再转发给视图（视图不直接订阅数据中心）。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class DataCenter<T> : IDataCenter where T : IData
    {
        //数据
        protected readonly Dictionary<long, T> datas = new();
        //数据id
        protected long id = 0;

        /// <summary>
        /// 获取数据
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        public virtual T GetData(long Id)
        {
            if (datas.TryGetValue(Id, out var Data))
            {
                return Data;
            }
            else
            {
                Debug.LogWarning($"未找到{typeof(T).Name}类型的数据，Id: {Id}");
                return default;
            }
        }

        /// <summary>
        /// 获取全部数据
        /// </summary>
        /// <returns></returns>
        public virtual Dictionary<long, T> GetAllData()
        {
            return datas;
        }

        /// <summary>
        /// 添加数据
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public virtual long AddData(T data)
        {
            if (data == null)
            {
                Debug.LogWarning($"无法添加空的{typeof(T).Name}类型的数据");
                return -1;
            }

            id++;
            data.SetId(id);
            datas.Add(id, data);
            return id;
        }

        /// <summary>
        /// 更新数据
        /// </summary>
        /// <param name="Id"></param>
        /// <param name="Data"></param>
        public virtual bool UpdateData(long Id, T Data)
        {
            if (datas.ContainsKey(Id))
            {
                datas[Id] = Data;
                //请确ID未被修改
                Data.SetId(Id);
                return true;
            }
            else
            {
                Debug.LogWarning($"未找到{typeof(T).Name}类型的数据，Id: {Id}, 无法更新");
                return false;
            }
        }

        /// <summary>
        /// 移除数据
        /// </summary>
        /// <param name="Id"></param>
        /// <returns>是否真的删掉了（false 表示本来就没有这条数据）</returns>
        public virtual bool RemoveData(long Id)
        {
            // 先取值再删：payload 要让订阅者拿到被移除的那条数据
            if (datas.TryGetValue(Id, out var removed))
            {
                datas.Remove(Id);
                return true;
            }

            Debug.LogWarning($"未找到{typeof(T).Name}类型的数据，Id: {Id}, 无法删除");
            return false;
        }

        /// <summary>
        /// 清空所有数据,id不重置
        /// </summary>
        public virtual void Clear()
        {
            datas.Clear();
        }

        /// <summary>
        /// 是否存在Id
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        public virtual bool HasId(long Id)
        {
            return datas.ContainsKey(Id);
        }

        // 根据数据返回Id
        public virtual long GetId(T Data)
        {
            foreach (var item in datas)
            {
                if (item.Value == Data)
                {
                    return item.Key;
                }
            }
            return -1;
        }

        /// <summary>
        /// 是否存在数据
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        public virtual bool HasData(long Id)
        {
            if(datas.ContainsKey(Id))
            {
                if(datas[Id]!= null)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// 是否存在数据
        /// </summary>
        /// <param name="Data"></param>
        /// <returns></returns>
        public virtual bool HasData(T Data)
        {
            return datas.ContainsValue(Data);
        }
    }


}
