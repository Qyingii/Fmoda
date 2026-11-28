using System;
using System.Collections.Generic;
using FModa.Enum;

namespace FModa.Data
{
    /// <summary>导出过滤。所有条目 id 都是包含 CenterCode 的合成 id。</summary>
    public sealed class DataExportOptions
    {
        /// <summary>null 表示全部已注册中心；空集合表示不导出任何中心。</summary>
        public IReadOnlyCollection<int> CenterCodes { get; set; }

        /// <summary>
        /// 可选的条目过滤：CenterCode → 要导出的合成 id。
        /// 选中但未在这里出现的中心导出全部条目；值为空集合时只导出中心状态，不导出条目。
        /// </summary>
        public IReadOnlyDictionary<int, IReadOnlyCollection<long>> ItemIdsByCenter { get; set; }
    }

    /// <summary>导入过滤与写入方式。</summary>
    public sealed class DataImportOptions
    {
        /// <summary>null 表示文件中的全部中心；空集合表示不导入任何中心。</summary>
        public IReadOnlyCollection<int> CenterCodes { get; set; }

        /// <summary>默认 Replace；Append 会分配新 id，并通过结果返回新旧 id 映射。</summary>
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
        public IReadOnlyList<int> ImportedCenterCodes { get; }

        /// <summary>
        /// Append 模式的映射：CenterCode →（文件中的原 id → 本次分配的新 id）。
        /// Replace 模式为空。通用存档不会自动改写条目内部的 id 引用。
        /// </summary>
        public IReadOnlyDictionary<int, IReadOnlyDictionary<long, long>> IdMappings { get; }

        internal DataImportResult(
            bool success,
            string filePath,
            string error,
            IReadOnlyList<int> codes,
            IReadOnlyDictionary<int, IReadOnlyDictionary<long, long>> idMappings)
        {
            Success = success;
            FilePath = filePath;
            Error = error;
            ImportedCenterCodes = codes ?? Array.Empty<int>();
            IdMappings = idMappings ?? new Dictionary<int, IReadOnlyDictionary<long, long>>();
        }
    }
}
