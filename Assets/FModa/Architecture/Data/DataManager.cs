using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using FModa.Enum;
using Newtonsoft.Json;

namespace FModa.Data
{
    /// <summary>
    /// 数据管理器：按具体类型自动注册和获取数据中心，并提供数据中心存档。
    /// 存档保存 sourceId 用于重建引用；Replace 和 Append 都重新发号。
    /// </summary>
    public static class DataManager
    {
        private const string SaveFormat = "FModa.DataCenterSave";
        private const int CurrentSaveVersion = 3;

        private static readonly JsonSerializerSettings SaveJsonSettings = new()
        {
            TypeNameHandling = TypeNameHandling.None,
        };

        private static readonly Dictionary<Type, IDataCenter> DataCenters = new();
        private static int dataCenterId = 1;

        #region 数据中心注册和获取

        /// <summary>注册一个新的数据中心；同类型重复注册会失败，正常获取请使用 Get。</summary>
        public static IDataCenter Register<T>() where T : IDataCenter, new()
        {
            if (DataCenters.ContainsKey(typeof(T)))
                throw new InvalidOperationException($"数据中心 {typeof(T).FullName} 已注册");
            if (dataCenterId > DataCenterId.MaxCenterCode)
                throw new InvalidOperationException("数据中心数量已超出可用 Code 上限");

            T instance = new T();
            instance.Init(dataCenterId++);
            DataCenters.Add(typeof(T), instance);
            return instance;
        }

        /// <summary>获取指定数据中心；首次访问时自动注册。</summary>
        public static T Get<T>() where T : IDataCenter, new()
        {
            if (DataCenters.TryGetValue(typeof(T), out var dataCenter))
                return (T)dataCenter;

            return (T)Register<T>();
        }

        /// <summary>重置所有已注册中心并保留其当前 Code；未注册的中心仍保持按需创建。</summary>
        public static void PreWarm()
        {
            var current = new List<IDataCenterSaveTarget>(DataCenters.Count);
            foreach (var center in DataCenters.Values)
                current.Add((IDataCenterSaveTarget)center);
            current.Sort((a, b) => a.CenterCode.CompareTo(b.CenterCode));

            var reset = new Dictionary<Type, IDataCenter>(DataCenters.Count);
            foreach (var center in current)
            {
                var replacement = CreateCenter(center.GetType(), center.CenterCode);
                reset.Add(center.GetType(), (IDataCenter)replacement);
            }

            DataCenters.Clear();
            foreach (var pair in reset)
                DataCenters.Add(pair.Key, pair.Value);
        }

        #endregion

        #region 导出与导入

        /// <summary>将当前已注册的数据中心保存为 JSON；保存 sourceId 用于引用映射，但不保存本地计数器。</summary>
        public static DataSaveResult ExportToFile(string path, DataExportOptions options = null)
        {
            string fullPath = path;

            try
            {
                if (string.IsNullOrWhiteSpace(path))
                    throw new ArgumentException("存档路径不能为空", nameof(path));

                fullPath = Path.GetFullPath(path);
                var centers = ResolveExportCenters(options);
                var codes = new List<int>(centers.Count);

                if (centers.Count == 0)
                    return new DataSaveResult(true, fullPath, null, codes);

                var save = new DataSaveFile
                {
                    format = SaveFormat,
                    formatVersion = CurrentSaveVersion,
                    savedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                };

                foreach (var center in centers)
                {
                    IReadOnlyCollection<long> itemIds = null;
                    if (options?.ItemIdsByCenter != null)
                        options.ItemIdsByCenter.TryGetValue(center.CenterCode, out itemIds);

                    save.centers.Add(center.Export(itemIds));
                    codes.Add(center.CenterCode);
                }

                string json = JsonConvert.SerializeObject(save, Formatting.Indented, SaveJsonSettings);
                WriteSaveFile(fullPath, json);
                return new DataSaveResult(true, fullPath, null, codes);
            }
            catch (Exception e)
            {
                return new DataSaveResult(false, fullPath, e.Message, Array.Empty<int>());
            }
        }

        /// <summary>
        /// 按数据中心类型匹配当前已注册中心，再根据文件中的数据顺序重新发号。
        /// 所有候选中心准备成功后才替换当前实例，失败时现有数据不变。
        /// </summary>
        public static DataImportResult ImportFromFile(string path, DataImportOptions options = null)
        {
            string fullPath = path;

            try
            {
                if (string.IsNullOrWhiteSpace(path))
                    throw new ArgumentException("存档路径不能为空", nameof(path));

                fullPath = Path.GetFullPath(path);
                var mode = options?.Mode ?? DataImportMode.Replace;
                if (mode != DataImportMode.Replace && mode != DataImportMode.Append)
                    throw new InvalidDataException($"未知的数据导入方式：{mode}");

                string json = File.ReadAllText(fullPath);
                var save = JsonConvert.DeserializeObject<DataSaveFile>(json, SaveJsonSettings);
                var fileCenters = ValidateAndIndexSave(save);
                var requests = ResolveImportRequests(fileCenters, options);
                var codes = new List<int>(requests.Count);

                if (requests.Count == 0)
                    return new DataImportResult(true, fullPath, null, codes);

                var pending = new List<PendingImport>(requests.Count);
                var idMappings = new Dictionary<int, Dictionary<long, long>>();
                foreach (var request in requests)
                {
                    var current = request.Current;
                    var candidate = CreateCenter(current.GetType(), current.CenterCode);

                    // Append 先深拷贝当前运行时数据并保留其运行时 id，再为导入数据重新发号。
                    if (mode == DataImportMode.Append)
                        candidate.CopyFrom(current);

                    var importedMappings = candidate.Import(request.Source);
                    var centerMappings = new Dictionary<long, long>(importedMappings.Count);
                    foreach (var mapping in importedMappings)
                        centerMappings.Add(mapping.SourceId, mapping.NewId);
                    idMappings.Add(request.Source.centerCode, centerMappings);

                    pending.Add(new PendingImport(current.GetType(), candidate));
                    codes.Add(current.CenterCode);
                }

                // 所有中心完成发号后统一修复引用；此阶段仍只修改候选实例。
                var remapContext = new DataIdRemapContext(idMappings);
                foreach (var item in pending)
                    item.Center.RemapImportedReferences(remapContext);

                // 所有导入及引用修复成功后才能替换当前实例。
                foreach (var item in pending)
                    DataCenters[item.CenterType] = (IDataCenter)item.Center;

                return new DataImportResult(true, fullPath, null, codes);
            }
            catch (Exception e)
            {
                return new DataImportResult(false, fullPath, e.Message, Array.Empty<int>());
            }
        }

        #endregion
        #region 私有方法
        private static List<IDataCenterSaveTarget> ResolveExportCenters(DataExportOptions options)
        {
            var selectedCodes = new HashSet<int>();
            if (options?.CenterCodes != null)
            {
                foreach (int code in options.CenterCodes)
                    selectedCodes.Add(code);
            }

            var result = new List<IDataCenterSaveTarget>();
            var resultCodes = new HashSet<int>();
            foreach (var center in DataCenters.Values)
            {
                var target = (IDataCenterSaveTarget)center;
                if (options?.CenterCodes != null && !selectedCodes.Contains(target.CenterCode))
                    continue;

                result.Add(target);
                resultCodes.Add(target.CenterCode);
            }

            if (options?.CenterCodes != null)
            {
                foreach (int code in selectedCodes)
                {
                    if (!resultCodes.Contains(code))
                        throw new InvalidDataException($"数据中心 Code {code} 尚未注册，无法导出");
                }
            }

            if (options?.ItemIdsByCenter != null)
            {
                foreach (int code in options.ItemIdsByCenter.Keys)
                {
                    if (!resultCodes.Contains(code))
                        throw new InvalidDataException($"条目过滤包含未选中的数据中心 Code {code}");
                }
            }

            result.Sort((a, b) => a.CenterCode.CompareTo(b.CenterCode));
            return result;
        }

        private static Dictionary<int, DataCenterSave> ValidateAndIndexSave(DataSaveFile save)
        {
            if (save == null)
                throw new InvalidDataException("存档内容为空或 JSON 无法解析");
            if (!string.Equals(save.format, SaveFormat, StringComparison.Ordinal))
                throw new InvalidDataException($"不是 {SaveFormat} 存档");
            if (save.formatVersion != CurrentSaveVersion)
                throw new InvalidDataException(
                    $"不支持的存档版本 {save.formatVersion}，当前仅支持 {CurrentSaveVersion}");
            if (save.centers == null)
                throw new InvalidDataException("存档缺少 centers");

            var result = new Dictionary<int, DataCenterSave>();
            var types = new HashSet<string>(StringComparer.Ordinal);
            foreach (var center in save.centers)
            {
                if (center == null || string.IsNullOrWhiteSpace(center.centerType))
                    throw new InvalidDataException("存档包含空中心或缺少 centerType");
                if (!result.TryAdd(center.centerCode, center))
                    throw new InvalidDataException($"存档重复包含数据中心 Code {center.centerCode}");
                if (!types.Add(center.centerType))
                    throw new InvalidDataException($"存档重复包含数据中心类型 {center.centerType}");
            }

            return result;
        }

        private static List<ImportRequest> ResolveImportRequests(
            Dictionary<int, DataCenterSave> fileCenters,
            DataImportOptions options)
        {
            var selectedCodes = new List<int>();
            if (options?.CenterCodes == null)
            {
                selectedCodes.AddRange(fileCenters.Keys);
            }
            else
            {
                var unique = new HashSet<int>();
                foreach (int code in options.CenterCodes)
                    unique.Add(code);
                selectedCodes.AddRange(unique);
            }
            selectedCodes.Sort();

            var result = new List<ImportRequest>(selectedCodes.Count);
            var targetTypes = new HashSet<Type>();
            foreach (int code in selectedCodes)
            {
                if (!fileCenters.TryGetValue(code, out var source))
                    throw new InvalidDataException($"存档不包含选中的数据中心 Code {code}");

                var current = FindRegisteredCenter(source.centerType);
                if (current == null)
                    throw new InvalidDataException(
                        $"数据中心类型 {source.centerType} 尚未注册；请先通过 DataManager.Get<T>() 获取该模块的数据中心");
                if (!targetTypes.Add(current.GetType()))
                    throw new InvalidDataException($"多个存档中心不能导入同一个运行时类型 {source.centerType}");

                result.Add(new ImportRequest(current, source));
            }

            return result;
        }

        private static IDataCenterSaveTarget FindRegisteredCenter(string centerType)
        {
            foreach (var center in DataCenters.Values)
            {
                if (string.Equals(center.GetType().FullName, centerType, StringComparison.Ordinal))
                    return (IDataCenterSaveTarget)center;
            }

            return null;
        }

        private static IDataCenterSaveTarget CreateCenter(Type centerType, int centerCode)
        {
            var instance = Activator.CreateInstance(centerType) as IDataCenter;
            if (instance == null)
                throw new InvalidDataException($"无法创建已注册的数据中心类型 {centerType.FullName}");

            instance.Init(centerCode);
            return (IDataCenterSaveTarget)instance;
        }

        private static void WriteSaveFile(string fullPath, string json)
        {
            string directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(directory))
                throw new InvalidDataException($"无法确定存档目录：{fullPath}");

            Directory.CreateDirectory(directory);
            string tempPath = fullPath + ".tmp";
            string backupPath = fullPath + ".bak";

            try
            {
                File.WriteAllText(tempPath, json, new UTF8Encoding(false));
                if (File.Exists(fullPath))
                {
                    File.Copy(fullPath, backupPath, true);
                    File.Replace(tempPath, fullPath, null);
                }
                else
                {
                    File.Move(tempPath, fullPath);
                }
            }
            catch
            {
                try
                {
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);
                }
                catch
                {
                    // 保留原始异常；临时文件清理失败不影响旧存档。
                }

                throw;
            }
        }
    #endregion
        private sealed class ImportRequest
        {
            public readonly IDataCenterSaveTarget Current;
            public readonly DataCenterSave Source;

            public ImportRequest(IDataCenterSaveTarget current, DataCenterSave source)
            {
                Current = current;
                Source = source;
            }
        }

        private sealed class PendingImport
        {
            public readonly Type CenterType;
            public readonly IDataCenterSaveTarget Center;

            public PendingImport(Type centerType, IDataCenterSaveTarget center)
            {
                CenterType = centerType;
                Center = center;
            }
        }
    }
}
