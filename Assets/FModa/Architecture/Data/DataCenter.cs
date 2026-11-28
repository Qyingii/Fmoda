using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace FModa.Data
{
    /// <summary>
    /// 泛型数据中心
    ///
    /// id 规则：
    ///   - 每个数据中心的 Code 由 DataManager 在注册时自动分配，仅用于当前运行时的 id 隔离
    ///   - 存储 key 是「Code + 本地自增 id」合成的 long，位布局见 DataCenterId
    ///   - 存档保存 sourceId 用于重建引用，但不保存计数器；导入时统一重新发号
    ///
    /// id 保护：
    ///   - 数据只有在未入册（Id = -1）时才能 AddData，避免同一实例被两个中心反复改号
    ///   - 数据离开中心（RemoveData / Clear）后 Id 立即失效为 -1
    ///   - UpdateData 只接受「新数据」或「本来就在该 Id 下的数据」
    ///
    /// 本类只负责存储与最小校验，不广播任何生命周期事件；业务事件由模块负责。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class DataCenter<T> : IDataCenter, IDataCenterSaveTarget where T : IData
    {
        //数据，key 为合成 id
        protected readonly Dictionary<long, T> datas = new();

        //本地自增 id（不含 Code）
        protected long id = 0;

        /// <summary>当前运行时的数据中心 Code，由 DataManager 自动分配。</summary>
        public int CenterCode { get; private set; }

        private bool initialized;

        /// <summary>初始化运行时 Code；一个实例只能初始化一次。</summary>
        public void Init(int centerCode)
        {
            if (initialized)
                throw new InvalidOperationException($"数据中心 {GetType().Name} 已初始化，不能重复分配 Code");
            if (!DataCenterId.IsValidCenterCode(centerCode))
                throw new ArgumentOutOfRangeException(nameof(centerCode), centerCode,
                    $"数据中心 Code 必须在 0 ~ {DataCenterId.MaxCenterCode} 之间");

            CenterCode = centerCode;
            initialized = true;
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
                Data = null;
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
                return null;
            }

            if (TryGetData(Id, out var Data))
            {
                return Data;
            }

            Debug.LogWarning(
                $"未找到{typeof(T).Name}类型的数据，Id: {Id}（{DataCenterId.Describe(Id)}）");
            return null;
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

        DataCenterSave IDataCenterSaveTarget.Export(IReadOnlyCollection<long> itemIds)
            => ExportSnapshot(itemIds);

        void IDataCenterSaveTarget.CopyFrom(IDataCenterSaveTarget source)
            => CopyRuntimeState(source);

        IReadOnlyList<DataIdMapping> IDataCenterSaveTarget.Import(DataCenterSave snapshot)
            => ImportSnapshot(snapshot);

        void IDataCenterSaveTarget.RemapImportedReferences(DataIdRemapContext context)
        {
            foreach (var data in importedData)
                RemapImportedReferences(data, context);
            importedData.Clear();
        }

        private readonly List<T> importedData = new();

        /// <summary>导出完整快照，或只导出 itemIds 指定的当前运行时条目。</summary>
        private DataCenterSave ExportSnapshot(IReadOnlyCollection<long> itemIds)
        {
            HashSet<long> selected = null;
            if (itemIds != null)
            {
                selected = new HashSet<long>(itemIds);
                foreach (long itemId in selected)
                {
                    if (!TryGetData(itemId, out _))
                        throw new InvalidDataException(
                            $"数据中心 Code {CenterCode} 不包含要导出的条目 {DataCenterId.Describe(itemId)}");
                }
            }

            var snapshot = new DataCenterSave
            {
                centerCode = CenterCode,
                centerType = GetType().FullName,
            };

            var ordered = new List<KeyValuePair<long, T>>(datas);
            ordered.Sort((a, b) => a.Key.CompareTo(b.Key));
            foreach (var pair in ordered)
            {
                if (selected != null && !selected.Contains(pair.Key))
                    continue;
                if (pair.Value == null)
                    throw new InvalidDataException($"数据中心 Code {CenterCode} 包含空数据，无法导出");

                snapshot.items.Add(new DataItemSave
                {
                    sourceId = pair.Key,
                    data = SerializeData(pair.Value),
                });
            }

            return snapshot;
        }

        /// <summary>深拷贝当前运行时状态，供 Append 在候选实例上保留现有条目及其 id。</summary>
        private void CopyRuntimeState(IDataCenterSaveTarget source)
        {
            if (datas.Count != 0)
                throw new InvalidDataException($"数据中心 Code {CenterCode} 不是空实例，不能复制运行时状态");
            if (!(source is DataCenter<T> sourceCenter) || source.GetType() != GetType())
                throw new InvalidDataException($"不能把 {source.GetType().FullName} 复制到 {GetType().FullName}");
            if (source.CenterCode != CenterCode)
                throw new InvalidDataException("复制运行时状态时源中心与目标中心的 Code 不一致");

            var cloned = new List<KeyValuePair<long, T>>(sourceCenter.datas.Count);
            foreach (var pair in sourceCenter.datas)
            {
                if (!(pair.Value.Clone() is T data))
                    throw new InvalidDataException(
                        $"数据中心 Code {CenterCode} 的条目 {pair.Key} 无法克隆为 {typeof(T).Name}");

                data.SetId(pair.Key);
                cloned.Add(new KeyValuePair<long, T>(pair.Key, data));
            }

            foreach (var pair in cloned)
                datas.Add(pair.Key, pair.Value);

            id = sourceCenter.id;
            RebuildAfterRestore();
        }

        /// <summary>按文件顺序重新发号，并记录 sourceId → 新 id 供跨中心引用修复。</summary>
        private IReadOnlyList<DataIdMapping> ImportSnapshot(DataCenterSave snapshot)
        {
            var items = ValidateSnapshotItems(snapshot);
            var mappings = new List<DataIdMapping>(items.Count);

            foreach (var item in items)
            {
                var data = DeserializeData(item.data);
                ValidateImportedData(data);
                data.SetId(-1);

                long previousCounter = id;
                long newId = AddData(data);
                if (newId < 0 || id <= previousCounter || newId != Compose(id))
                    throw new InvalidDataException(
                        $"数据中心 Code {CenterCode} 导入数据时没有分配新的尾部 id；" +
                        "存档导入不允许静默合并到已有条目");

                mappings.Add(new DataIdMapping(item.sourceId, newId));
                importedData.Add(data);
            }

            return mappings;
        }

        private static IReadOnlyList<DataItemSave> ValidateSnapshotItems(DataCenterSave snapshot)
        {
            if (snapshot == null)
                throw new InvalidDataException("存档数据中心快照为空");
            if (!DataCenterId.IsValidCenterCode(snapshot.centerCode))
                throw new InvalidDataException($"存档中心 Code {snapshot.centerCode} 非法");
            if (snapshot.items == null)
                throw new InvalidDataException($"数据中心类型 {snapshot.centerType} 缺少 items");

            var sourceIds = new HashSet<long>();
            foreach (var item in snapshot.items)
            {
                if (item == null || item.data == null)
                    throw new InvalidDataException($"数据中心类型 {snapshot.centerType} 包含空条目");
                if (!DataCenterId.BelongsTo(item.sourceId, snapshot.centerCode)
                    || !DataCenterId.IsValidLocalId(DataCenterId.Local(item.sourceId)))
                {
                    throw new InvalidDataException(
                        $"条目 sourceId {item.sourceId} 不属于存档中心 Code {snapshot.centerCode}，或本地 id 非法");
                }
                if (!sourceIds.Add(item.sourceId))
                    throw new InvalidDataException(
                        $"数据中心类型 {snapshot.centerType} 重复包含 sourceId {item.sourceId}");
            }

            return snapshot.items;
        }

        /// <summary>默认按 T 序列化；特殊数据可重写以补充非默认序列化字段。</summary>
        protected virtual JObject SerializeData(T data)
        {
            var json = JObject.FromObject(data, CreateSaveSerializer());
            json.Remove("Id");
            return json;
        }

        /// <summary>默认按 T 反序列化；条目 id 随后由 AddData 重新分配。</summary>
        protected virtual T DeserializeData(JObject json)
        {
            var data = json.ToObject<T>(CreateSaveSerializer());
            if (data == null)
                throw new InvalidDataException($"数据中心 Code {CenterCode} 的条目反序列化为空");
            return data;
        }

        /// <summary>导入前的通用数据校验，子类可按自身不变量扩展。</summary>
        protected virtual void ValidateSaveData(T data)
        {
        }

        /// <summary>导入前校验；子类可拒绝会被自身 AddData 合并的业务键。</summary>
        protected virtual void ValidateImportedData(T data) => ValidateSaveData(data);

        /// <summary>
        /// 所有中心完成重新发号后调用。只处理本次导入的数据，Append 中的已有数据不会进入这里。
        /// 子类在此显式声明哪些 long 字段或 long 集合是其他条目的 id。
        /// </summary>
        protected virtual void RemapImportedReferences(T data, DataIdRemapContext context)
        {
        }

        /// <summary>复制当前运行时状态后重建子类额外索引。</summary>
        protected virtual void RebuildAfterRestore()
        {
        }

        private static JsonSerializer CreateSaveSerializer()
            => JsonSerializer.Create(new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.None,
            });

        /// <summary>
        /// 克隆一份数据并重新入册。原数据不存在或 Clone 失败时返回 -1。
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        public virtual long CloneData(long Id)
        {
            if (!TryGetData(Id, out var source))
            {
                Debug.LogWarning($"未找到{typeof(T).Name}类型的数据，Id: {Id}, 无法克隆");
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
