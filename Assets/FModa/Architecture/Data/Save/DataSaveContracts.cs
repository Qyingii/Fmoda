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
        public List<DataItemSave> items = new();
    }

    internal sealed class DataItemSave
    {
        /// <summary>导出时的运行时 id，仅用于导入时重建引用，不要求导入后继续沿用。</summary>
        public long sourceId;
        public JObject data;
    }

    internal readonly struct DataIdMapping
    {
        public readonly long SourceId;
        public readonly long NewId;

        public DataIdMapping(long sourceId, long newId)
        {
            SourceId = sourceId;
            NewId = newId;
        }
    }

    /// <summary>仅供 DataManager 编排存档使用，不向业务模块公开恢复能力。</summary>
    internal interface IDataCenterSaveTarget
    {
        int CenterCode { get; }
        DataCenterSave Export(IReadOnlyCollection<long> itemIds);
        void CopyFrom(IDataCenterSaveTarget source);
        IReadOnlyList<DataIdMapping> Import(DataCenterSave snapshot);
        void RemapImportedReferences(DataIdRemapContext context);
    }

    /// <summary>
    /// 导入期间的新旧 id 映射查询。sourceId 中的 CenterCode 指导出时的中心 Code，
    /// 因此即使当前运行时的 Code 不同，也能找到正确的目标数据。
    /// </summary>
    public sealed class DataIdRemapContext
    {
        private readonly Dictionary<int, Dictionary<long, long>> mappings;

        internal DataIdRemapContext(Dictionary<int, Dictionary<long, long>> mappings)
        {
            this.mappings = mappings;
        }

        /// <summary>重映射一个必需引用；映射不存在时让整次导入失败。</summary>
        public long Remap(long sourceId)
        {
            if (TryRemap(sourceId, out long newId))
                return newId;

            throw new System.IO.InvalidDataException(
                $"无法重映射 id {sourceId}（{DataCenterId.Describe(sourceId)}）：" +
                "存档未包含被引用的数据，或该数据未参与本次导入");
        }

        /// <summary>尝试重映射；可选引用可由具体数据中心决定缺失时如何处理。</summary>
        public bool TryRemap(long sourceId, out long newId)
        {
            newId = -1;
            if (sourceId < 0)
                return false;

            int sourceCenterCode = DataCenterId.Center(sourceId);
            if (mappings.TryGetValue(sourceCenterCode, out var centerMappings)
                && centerMappings.TryGetValue(sourceId, out newId))
            {
                return true;
            }

            return false;
        }
    }
}
