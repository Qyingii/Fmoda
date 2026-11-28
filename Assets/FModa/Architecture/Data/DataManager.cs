using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Conquer.Unit;
using FModa.Enum;
using FModa.Multilingual;
using FModa.SeedRandom;
using Newtonsoft.Json;

namespace FModa.Data
{
    /// <summary>
    /// 数据管理器：数据中心的统一注册入口，以及数据中心存档的导出入口。
    /// 存档只处理 DataCenter 状态，不处理模块私有状态、场景对象或视图。
    /// </summary>
    public static class DataManager
    {
        private const string SaveFormat = "FModa.DataCenterSave";
        private const int CurrentSaveVersion = 1;

        private static readonly JsonSerializerSettings SaveJsonSettings = new()
        {
            TypeNameHandling = TypeNameHandling.None,
        };

        private static readonly Dictionary<int, DataCenterRegistration> Registry = CreateRegistry();

        // 单位数据中心
        private static UnitDataCenter _unit;
        public static UnitDataCenter Unit => _unit ??= new UnitDataCenter(DataCenterCodes.Unit);

        // 多语言数据中心
        private static MultilingualDataCenter _multilingual;
        public static MultilingualDataCenter Multilingual
            => _multilingual ??= new MultilingualDataCenter(DataCenterCodes.Multilingual);

        // 带种子的随机数数据中心
        private static SeedRandomDataCenter _seedRandom;
        public static SeedRandomDataCenter SeedRandom
            => _seedRandom ??= new SeedRandomDataCenter(DataCenterCodes.SeedRandom);

        /// <summary>提前初始化全部数据中心；调用方仍须通过属性访问，不要缓存实例。</summary>
        public static void PreWarm()
        {
            _unit = new UnitDataCenter(DataCenterCodes.Unit);
            _multilingual = new MultilingualDataCenter(DataCenterCodes.Multilingual);
            _seedRandom = new SeedRandomDataCenter(DataCenterCodes.SeedRandom);
        }

        #region 导出与导入
        /// <summary>
        /// 将数据中心保存为 JSON。可通过 CenterCodes 和 ItemIdsByCenter 控制导出范围。
        /// 本方法同步执行，应在主线程且没有数据变更的步骤中调用。
        /// </summary>
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

                // 空选择是合法的无操作，不创建空存档覆盖旧文件。
                if (centers.Count == 0)
                    return new DataSaveResult(true, fullPath, null, codes);

                var save = new DataSaveFile
                {
                    format = SaveFormat,
                    formatVersion = CurrentSaveVersion,
                    savedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                };

                foreach (var registration in centers)
                {
                    IReadOnlyCollection<long> itemIds = null;
                    if (options?.ItemIdsByCenter != null)
                        options.ItemIdsByCenter.TryGetValue(registration.Code, out itemIds);

                    save.centers.Add(registration.GetOrCreate().Export(itemIds));
                    codes.Add(registration.Code);
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
        /// 从 JSON 导入数据中心。Replace 保留原 id 并整体替换；Append 重新发号并追加。
        /// 所有选中的中心都在候选实例上准备成功后才会替换当前实例，失败时内存不变。
        /// </summary>
        public static DataImportResult ImportFromFile(string path, DataImportOptions options = null)
        {
            string fullPath = path;
            var emptyMappings = new Dictionary<int, IReadOnlyDictionary<long, long>>();

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
                var centers = ResolveImportCenters(fileCenters, options);
                var codes = new List<int>(centers.Count);

                if (centers.Count == 0)
                    return new DataImportResult(true, fullPath, null, codes, emptyMappings);

                var pending = new List<PendingImport>(centers.Count);
                var mappings = mode == DataImportMode.Append
                    ? new Dictionary<int, IReadOnlyDictionary<long, long>>()
                    : emptyMappings;

                foreach (var registration in centers)
                {
                    var source = fileCenters[registration.Code];
                    var candidate = registration.Create();
                    IReadOnlyDictionary<long, long> idMap = null;

                    if (mode == DataImportMode.Append)
                    {
                        // 先复制当前中心，再在副本上追加；任何失败都不会触碰当前实例。
                        candidate.Restore(registration.GetOrCreate().Export(null));
                        idMap = candidate.Append(source);
                        mappings.Add(registration.Code, idMap);
                    }
                    else
                    {
                        candidate.Restore(source);
                    }

                    pending.Add(new PendingImport(registration, candidate));
                    codes.Add(registration.Code);
                }

                foreach (var item in pending)
                    item.Registration.Replace(item.Center);

                return new DataImportResult(true, fullPath, null, codes, mappings);
            }
            catch (Exception e)
            {
                return new DataImportResult(false, fullPath, e.Message, Array.Empty<int>(), emptyMappings);
            }
        }
        #endregion

        private static Dictionary<int, DataCenterRegistration> CreateRegistry()
        {
            var registry = new Dictionary<int, DataCenterRegistration>
            {
                {
                    DataCenterCodes.Unit,
                    new DataCenterRegistration(
                        DataCenterCodes.Unit,
                        () => (IDataCenterSaveTarget)Unit,
                        () => new UnitDataCenter(DataCenterCodes.Unit),
                        center => _unit = (UnitDataCenter)center)
                },
                {
                    DataCenterCodes.Multilingual,
                    new DataCenterRegistration(
                        DataCenterCodes.Multilingual,
                        () => (IDataCenterSaveTarget)Multilingual,
                        () => new MultilingualDataCenter(DataCenterCodes.Multilingual),
                        center => _multilingual = (MultilingualDataCenter)center)
                },
                {
                    DataCenterCodes.SeedRandom,
                    new DataCenterRegistration(
                        DataCenterCodes.SeedRandom,
                        () => (IDataCenterSaveTarget)SeedRandom,
                        () => new SeedRandomDataCenter(DataCenterCodes.SeedRandom),
                        center => _seedRandom = (SeedRandomDataCenter)center)
                },
            };

            return registry;
        }

        /// <summary>
        /// 根据 Code 排序并添加到 <see cref="result"/> 中。
        /// </summary>
        /// <param name="options"></param>
        /// <returns></returns>
        /// <exception cref="InvalidDataException"></exception>
        private static List<DataCenterRegistration> ResolveExportCenters(DataExportOptions options)
        {
            var result = new List<DataCenterRegistration>();
            var selected = new HashSet<int>();

            if (options?.CenterCodes == null)
            {
                foreach (var pair in Registry)
                    selected.Add(pair.Key);
            }
            else
            {
                foreach (int code in options.CenterCodes)
                {
                    if (!Registry.ContainsKey(code))
                        throw new InvalidDataException($"数据中心 Code {code} 未注册，无法导出");
                    selected.Add(code);
                }
            }

            if (options?.ItemIdsByCenter != null)
            {
                foreach (int code in options.ItemIdsByCenter.Keys)
                {
                    if (!selected.Contains(code))
                        throw new InvalidDataException($"条目过滤包含未选中的数据中心 Code {code}");
                }
            }

            AddSortedRegistrations(selected, result);
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
            foreach (var center in save.centers)
            {
                if (center == null)
                    throw new InvalidDataException("存档包含空的数据中心节点");
                if (!result.TryAdd(center.centerCode, center))
                    throw new InvalidDataException($"存档重复包含数据中心 Code {center.centerCode}");
            }

            return result;
        }

        private static List<DataCenterRegistration> ResolveImportCenters(
            Dictionary<int, DataCenterSave> fileCenters,
            DataImportOptions options)
        {
            var selected = new HashSet<int>();

            if (options?.CenterCodes == null)
            {
                foreach (int code in fileCenters.Keys)
                {
                    if (!Registry.ContainsKey(code))
                        throw new InvalidDataException($"存档包含未注册的数据中心 Code {code}");
                    selected.Add(code);
                }
            }
            else
            {
                foreach (int code in options.CenterCodes)
                {
                    if (!Registry.ContainsKey(code))
                        throw new InvalidDataException($"数据中心 Code {code} 未注册，无法导入");
                    if (!fileCenters.ContainsKey(code))
                        throw new InvalidDataException($"存档不包含选中的数据中心 Code {code}");
                    selected.Add(code);
                }
            }

            var result = new List<DataCenterRegistration>(selected.Count);
            AddSortedRegistrations(selected, result);
            return result;
        }

        private static void AddSortedRegistrations(
            HashSet<int> selected,
            List<DataCenterRegistration> result)
        {
            var codes = new List<int>(selected);
            codes.Sort();
            foreach (int code in codes)
                result.Add(Registry[code]);
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

        private sealed class DataCenterRegistration
        {
            public readonly int Code;
            public readonly Func<IDataCenterSaveTarget> GetOrCreate;
            public readonly Func<IDataCenterSaveTarget> Create;
            public readonly Action<IDataCenterSaveTarget> Replace;

            public DataCenterRegistration(
                int code,
                Func<IDataCenterSaveTarget> getOrCreate,
                Func<IDataCenterSaveTarget> create,
                Action<IDataCenterSaveTarget> replace)
            {
                Code = code;
                GetOrCreate = getOrCreate;
                Create = create;
                Replace = replace;
            }
        }

        private sealed class PendingImport
        {
            public readonly DataCenterRegistration Registration;
            public readonly IDataCenterSaveTarget Center;

            public PendingImport(DataCenterRegistration registration, IDataCenterSaveTarget center)
            {
                Registration = registration;
                Center = center;
            }
        }
    }

    /// <summary>数据中心 Code：显式、稳定、只增不改，不得复用或换绑类型。</summary>
    public static class DataCenterCodes
    {
        public const int Unit = 1;
        public const int Multilingual = 2;
        public const int SeedRandom = 3;
    }
}
