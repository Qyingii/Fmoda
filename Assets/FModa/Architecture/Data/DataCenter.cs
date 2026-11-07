using System.Collections.Generic;
using UnityEngine;

namespace FModa.Data
{
    /// <summary>
    /// 泛型数据中心
    ///
    /// id 规则：
    ///   - 每个数据中心拥有一个稳定的 Code（见 DataManager.DataCenterCodes）
    ///   - 存储 key 是「Code + 本地自增 id」合成的 long，位布局见 DataCenterId
    ///   - Code = 0 时合成 id 与旧版本逐位一致，未迁移的工程行为不变
    ///
    /// id 保护：
    ///   - 数据只有在未入册（Id = -1）时才能 AddData，避免同一实例被两个中心反复改号
    ///   - 数据离开中心（RemoveData / Clear）后 Id 立即失效为 -1
    ///   - UpdateData 只接受「新数据」或「本来就在该 Id 下的数据」
    ///
    /// 本类只负责存储与最小校验，不广播任何生命周期事件；业务事件由模块负责。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class DataCenter<T> : IDataCenter where T : IData
    {
        //数据，key 为合成 id
        protected readonly Dictionary<long, T> datas = new();

        //本地自增 id（不含 Code）
        protected long id = 0;

        /// <summary>本数据中心的数据中心 Code；0 表示未分区（旧行为）。</summary>
        public int CenterCode { get; }

        public DataCenter(int centerCode)
        {
            if (!DataCenterId.IsValidCenterCode(centerCode))
            {
                Debug.LogError(
                    $"[DataCenter] {GetType().Name} 的 Code {centerCode} 超出 " +
                    $"0 ~ {DataCenterId.MaxCenterCode}，已回退为 0");
                centerCode = 0;
            }

            CenterCode = centerCode;
            DataCenterCodeRegistry.Claim(centerCode, GetType());

#if UNITY_EDITOR
            if (centerCode == 0)
                DataCenterCodeRegistry.WarnIfUnregistered(GetType());
#endif
        }

        /// <summary>把本地 id 合成为存储 key。</summary>
        protected long Compose(long localId) => DataCenterId.Compose(CenterCode, localId);

        /// <summary>取出存储 key 的本地 id 部分，用于 UI/日志展示。</summary>
        public long ToLocalId(long composedId) => DataCenterId.Local(composedId);

        /// <summary>该 key 是否属于本数据中心。跨中心传错 id 时返回 false。</summary>
        public bool BelongsToCenter(long composedId)
            => DataCenterId.BelongsTo(composedId, CenterCode);

        /// <summary>
        /// 安全查询：id 不属于本中心、未找到或数据为 null 时返回 false，不打警告。
        /// </summary>
        public virtual bool TryGetData(long Id, out T Data)
        {
            if (!BelongsToCenter(Id))
            {
                Data = default;
                return false;
            }

            return datas.TryGetValue(Id, out Data) && Data != null;
        }

        /// <summary>
        /// 获取数据
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        public virtual T GetData(long Id)
        {
            if (!BelongsToCenter(Id))
            {
                Debug.LogWarning(
                    $"{typeof(T).Name} 收到不属于本中心的数据 Id: {Id}" +
                    $"（本中心 Code={CenterCode}，{DataCenterId.Describe(Id)}）");
                return default;
            }

            if (TryGetData(Id, out var Data))
            {
                return Data;
            }

            Debug.LogWarning(
                $"未找到{typeof(T).Name}类型的数据，Id: {Id}（{DataCenterId.Describe(Id)}）");
            return default;
        }

        /// <summary>
        /// 获取全部数据。注意：字典的 key 是合成 id，展示时用 ToLocalId 取本地序号。
        /// </summary>
        /// <returns></returns>
        public virtual Dictionary<long, T> GetAllData()
        {
            return datas;
        }

        /// <summary>
        /// 添加数据。只接受尚未入册（Id = -1）的数据，返回合成 id；失败返回 -1。
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

            // id 保护：数据已经属于某个中心/某个 id 时，不允许再次入册
            if (data.Id != -1)
            {
                Debug.LogError(
                    $"无法添加{typeof(T).Name}：该数据已拥有 Id {data.Id}" +
                    $"（{DataCenterId.Describe(data.Id)}），不能重复入册");
                return -1;
            }

            long next = id + 1;
            if (!DataCenterId.IsValidLocalId(next))
            {
                Debug.LogError(
                    $"{typeof(T).Name} 的本地 id 已达上限 {DataCenterId.MaxLocalId}，无法继续添加");
                return -1;
            }

            id = next;
            long composedId = Compose(id);
            data.SetId(composedId);
            datas.Add(composedId, data);
            return composedId;
        }

        /// <summary>
        /// 更新数据。
        /// 只接受「新数据（Id = -1，会绑定到目标 Id）」或「本来就在该 Id 下的数据」，
        /// 防止把别的中心/别的 Id 的数据静默改号塞进来。
        /// </summary>
        /// <param name="Id"></param>
        /// <param name="Data"></param>
        public virtual bool UpdateData(long Id, T Data)
        {
            if (Data == null)
            {
                Debug.LogWarning($"无法把空数据写入{typeof(T).Name}，Id: {Id}");
                return false;
            }

            if (!BelongsToCenter(Id))
            {
                Debug.LogWarning(
                    $"{typeof(T).Name} 收到不属于本中心的数据 Id: {Id}" +
                    $"（本中心 Code={CenterCode}，{DataCenterId.Describe(Id)}）");
                return false;
            }

            if (!datas.ContainsKey(Id))
            {
                Debug.LogWarning($"未找到{typeof(T).Name}类型的数据，Id: {Id}, 无法更新");
                return false;
            }

            if (Data.Id != -1 && Data.Id != Id)
            {
                Debug.LogError(
                    $"无法更新{typeof(T).Name}：传入数据的 Id {Data.Id}" +
                    $"（{DataCenterId.Describe(Data.Id)}）与目标 Id {Id} 不一致");
                return false;
            }

            datas[Id] = Data;
            Data.SetId(Id);
            return true;
        }

        /// <summary>
        /// 移除数据。移除后这条数据的 Id 会失效为 -1，继续持有它无法再冒充有效数据。
        /// </summary>
        /// <param name="Id"></param>
        /// <returns>是否真的删掉了（false 表示本来就没有这条数据）</returns>
        public virtual bool RemoveData(long Id)
        {
            if (!BelongsToCenter(Id))
            {
                Debug.LogWarning(
                    $"{typeof(T).Name} 收到不属于本中心的数据 Id: {Id}" +
                    $"（本中心 Code={CenterCode}，{DataCenterId.Describe(Id)}）");
                return false;
            }

            if (datas.TryGetValue(Id, out var removed))
            {
                datas.Remove(Id);
                removed?.SetId(-1);
                return true;
            }

            Debug.LogWarning($"未找到{typeof(T).Name}类型的数据，Id: {Id}, 无法删除");
            return false;
        }

        /// <summary>
        /// 清空所有数据。所有数据的 Id 会失效为 -1；本地 id 计数器不重置，
        /// 同一实例内已经发出过的 id 不会再分配。
        /// </summary>
        public virtual void Clear()
        {
            foreach (var data in datas.Values)
            {
                data?.SetId(-1);
            }

            datas.Clear();
        }

        /// <summary>
        /// 是否存在该 Id（不属于本中心的 Id 一律视为不存在）
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        public virtual bool HasId(long Id)
        {
            return BelongsToCenter(Id) && datas.ContainsKey(Id);
        }

        // 根据数据返回 Id
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
            return TryGetData(Id, out _);
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

        /// <summary>
        /// 克隆一份数据并重新入册。原数据不存在或 Clone 失败时返回 -1。
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        public virtual long CloneData(long Id)
        {
            if (!TryGetData(Id, out var source))
            {
                return -1;
            }

            var clone = source.Clone();
            if (!(clone is T typedClone))
            {
                Debug.LogWarning($"{typeof(T).Name} 的 Clone() 没有返回同类型数据，无法克隆 Id {Id}");
                return -1;
            }

            typedClone.SetId(-1);
            return AddData(typedClone);
        }
    }
}
