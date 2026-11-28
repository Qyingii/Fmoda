using System;
using System.Collections.Generic;
using FModa.Enum;

namespace FModa.Data
{
    /// <summary>导出过滤。条目 id 只用于选择当前运行时数据，不会写入存档。</summary>
    public sealed class DataExportOptions
    {
        /// <summary>null 表示全部已注册中心；空集合表示不导出任何中心。</summary>
        public IReadOnlyCollection<int> CenterCodes { get; set; }

        /// <summary>
        /// 可选的条目过滤：当前 CenterCode → 要导出的合成 id。
        /// 选中但未在这里出现的中心导出全部条目；值为空集合时导出该中心但不导出条目。
        /// </summary>
        public IReadOnlyDictionary<int, IReadOnlyCollection<long>> ItemIdsByCenter { get; set; }
    }

    /// <summary>导入过滤与写入方式。</summary>
    public sealed class DataImportOptions
    {
        /// <summary>按存档中的 centerCode 过滤；null 表示全部，空集合表示不导入。</summary>
        public IReadOnlyCollection<int> CenterCodes { get; set; }

        /// <summary>Replace 和 Append 都会忽略旧 id，根据数据顺序重新发号。</summary>
        public DataImportMode Mode { get; set; } = DataImportMode.Replace;
    }

    public sealed class DataSaveResult
    {
        public bool Success { get; }
        public string FilePath { get; }
        public string Error { get; }
        public IReadOnlyList<int> ExportedCenterCodes { get; }

        internal DataSaveResult(bool success, string filePath, string error, IReadOnlyList<int> codes)
        {
            Success = success;
            FilePath = filePath;
            Error = error;
            ExportedCenterCodes = codes ?? Array.Empty<int>();
        }
    }

    public sealed class DataImportResult
    {
        public bool Success { get; }
        public string FilePath { get; }
        public string Error { get; }

        /// <summary>本次变更中心在当前运行时使用的 CenterCode。</summary>
        public IReadOnlyList<int> ImportedCenterCodes { get; }

        internal DataImportResult(bool success, string filePath, string error, IReadOnlyList<int> codes)
        {
            Success = success;
            FilePath = filePath;
            Error = error;
            ImportedCenterCodes = codes ?? Array.Empty<int>();
        }
    }
}
