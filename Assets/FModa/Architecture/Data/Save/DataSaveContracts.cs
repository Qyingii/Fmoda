using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace FModa.Data
{
    /// <summary>磁盘存档根对象。字段名是稳定的存档契约，不要随意改名。</summary>
    internal sealed class DataSaveFile
    {
        public string format;
        public int formatVersion;
        public string savedAtUtc;
        public List<DataCenterSave> centers = new();
    }

    /// <summary>一个数据中心的完整或按条目过滤后的快照。</summary>
    internal sealed class DataCenterSave
    {
        public int centerCode;
        public string centerType;
        public long lastLocalId;
        public List<DataItemSave> items = new();
    }

    /// <summary>条目 id 是唯一权威来源，data 内不重复保存 IData.Id。</summary>
    internal sealed class DataItemSave
    {
        public long id;
        public JObject data;
    }

    /// <summary>仅供 DataManager 编排存档使用，不向业务模块公开恢复能力。</summary>
    internal interface IDataCenterSaveTarget
    {
        DataCenterSave Export(IReadOnlyCollection<long> itemIds);
        void Restore(DataCenterSave snapshot);
        IReadOnlyDictionary<long, long> Append(DataCenterSave snapshot);
    }
}
