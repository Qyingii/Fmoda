using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FModa.Data;
using UnityEngine;

namespace FModa.Multilingual
{
    /// <summary>
    /// 多语言模块。
    ///
    /// 语种来源有两层：
    /// 1. 内置语言包：Resources/Multilingual/*.json，随包发布、只读；
    /// 2. 自定义语言包：persistentDataPath/Multilingual/*.json，运行时写入、可覆盖内置条目。
    ///
    /// 没有保存的玩家手动选择时，按 Application.systemLanguage 自动匹配；
    /// 匹配不到则回退到 DefaultLanguageCode（默认 en），再回退到第一个可用语种。
    /// 只有玩家主动调用 SetLanguage 时，选择才写入 PlayerPrefs；
    /// 自动选择每次启动都会重新计算，系统语言变化或新增语言包后可以自动跟随。
    ///
    /// 加载与选择在 Init() 时执行；若 Flow 忘记调用，首次 API 访问也会懒初始化。
    /// 这样 Flow 可以在 Init() 之前修改 AutoDetectSystemLanguage / DefaultLanguageCode。
    /// </summary>
    public class MultilingualManager : IMultilingualManager, IModule
    {
        private const string SaveKey = "FModa.Multilingual.Language";
        private const string BuiltinResourcesFolder = "Multilingual";
        private const string CustomFolderName = "Multilingual";
        private const string DefaultCode = "en";

        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        private readonly HashSet<string> _warnedMissingKeys = new(StringComparer.Ordinal);

        private string _currentLanguageCode;
        private string _defaultLanguageCode = DefaultCode;
        private bool _initialized;

        public Type InterfaceType => typeof(IMultilingualManager);

        /// <summary>全局基础服务，无每帧逻辑；Priority 仅参与模块统一排序。</summary>
        public int Priority => 5;

        public event Action<string, string> OnLanguageChanged;
        public event Action<string> OnLanguageAdded;
        public event Action<string> OnLanguageRemoved;

        public string CurrentLanguageCode
        {
            get
            {
                EnsureInitialized();
                return _currentLanguageCode;
            }
        }

        public string DefaultLanguageCode
        {
            get => _defaultLanguageCode;
            set => _defaultLanguageCode = NormalizeCode(value) ?? DefaultCode;
        }

        public bool AutoDetectSystemLanguage { get; set; } = true;

        // 每次用属性取，绝不缓存：DataManager.PreWarm() 会整体替换实例。
        private MultilingualDataCenter Data => DataManager.Multilingual;

        public void Init() => EnsureInitialized();

        public void OnDestroy()
        {
            OnLanguageChanged = null;
            OnLanguageAdded = null;
            OnLanguageRemoved = null;
            _warnedMissingKeys.Clear();

            // 模块销毁后清空自己的数据中心；重新创建时从 Resources/存档重新加载。
            Data.Clear();

            _currentLanguageCode = null;
            _initialized = false;
        }

        public void OnApplicationQuit() => PlayerPrefs.Save();

        public IReadOnlyList<LanguageInfo> GetAvailableLanguages()
        {
            EnsureInitialized();

            var result = new List<LanguageInfo>();
            foreach (var language in Data.GetAllLanguages())
                result.Add(new LanguageInfo(language.code, language.displayName, language.isBuiltin));

            return result;
        }

        public bool HasLanguage(string code)
        {
            EnsureInitialized();
            return Data.HasLanguage(NormalizeCode(code));
        }

        public bool SetLanguage(SystemLanguage code)
        {
            return SetLanguage(MapSystemLanguage(code));
        }

        public bool SetLanguage(string code)
        {
            EnsureInitialized();
            return SetLanguageInternal(code, save: true, notify: true);
        }

        public string GetText(string key)
        {
            EnsureInitialized();

            if (string.IsNullOrEmpty(key))
                return string.Empty;

            if (TryGetText(_currentLanguageCode, key, out var text))
                return text;

            if (TryGetText(_defaultLanguageCode, key, out text))
                return text;

            WarnMissingKeyOnce(key);
            return key;
        }

        public string GetText(string key, params object[] args)
        {
            var text = GetText(key);
            if (args == null || args.Length == 0)
                return text;

            try
            {
                return string.Format(text, args);
            }
            catch (FormatException e)
            {
                Debug.LogWarning($"[Multilingual] 文本格式化失败 key={key}: {e.Message}");
                return text;
            }
        }

        public bool HasKey(string key)
        {
            EnsureInitialized();

            if (string.IsNullOrEmpty(key))
                return false;

            return TryGetText(_currentLanguageCode, key, out _)
                || TryGetText(_defaultLanguageCode, key, out _);
        }

        public bool AddLanguage(
            string code,
            string displayName,
            IReadOnlyDictionary<string, string> entries,
            bool persist = false)
        {
            EnsureInitialized();

            var normalized = NormalizeCode(code);
            if (string.IsNullOrEmpty(normalized))
            {
                Debug.LogWarning("[Multilingual] AddLanguage 失败：code 为空");
                return false;
            }

            var existing = Data.GetByCode(normalized);
            var data = new LanguageData
            {
                code = normalized,
                displayName = string.IsNullOrWhiteSpace(displayName)
                    ? existing?.displayName ?? normalized
                    : displayName.Trim(),
                isBuiltin = false
            };

            if (entries != null)
            {
                foreach (var pair in entries)
                {
                    if (string.IsNullOrEmpty(pair.Key))
                        continue;

                    data.entries[pair.Key] = pair.Value ?? string.Empty;
                    data.customKeys.Add(pair.Key);
                }
            }

            if (Data.AddData(data) < 0)
                return false;

            var stored = Data.GetByCode(normalized);
            if (persist && !PersistLanguage(stored))
                Debug.LogWarning($"[Multilingual] 语种 {normalized} 已在本次运行生效，但持久化失败");

            // 没有任何可用语种时（例如空项目首次接入），把新语种设为当前语种。
            if (string.IsNullOrEmpty(_currentLanguageCode))
                SetLanguageInternal(normalized, save: false, notify: false);

            OnLanguageAdded?.Invoke(normalized);
            NotifyIfCurrentLanguageUpdated(normalized);
            return true;
        }

        public bool AddLanguageFromJson(string json, bool persist = false)
        {
            EnsureInitialized();

            if (!TryParseLanguageJson(json, null, out var data, out var error))
            {
                Debug.LogWarning($"[Multilingual] AddLanguageFromJson 失败：{error}");
                return false;
            }

            // JSON 没写 displayName 时，已有语种沿用旧名称，新语种用 code。
            data.displayName ??= Data.GetByCode(data.code)?.displayName ?? data.code;

            foreach (var key in data.entries.Keys)
                data.customKeys.Add(key);

            if (Data.AddData(data) < 0)
                return false;

            var stored = Data.GetByCode(data.code);
            if (persist && !PersistLanguage(stored))
                Debug.LogWarning($"[Multilingual] 语种 {data.code} 已在本次运行生效，但持久化失败");

            if (string.IsNullOrEmpty(_currentLanguageCode))
                SetLanguageInternal(data.code, save: false, notify: false);

            OnLanguageAdded?.Invoke(data.code);
            NotifyIfCurrentLanguageUpdated(data.code);
            return true;
        }

        public bool RemoveLanguage(string code, bool deletePersisted = false)
        {
            EnsureInitialized();

            var normalized = NormalizeCode(code);
            var data = Data.GetByCode(normalized);
            if (data == null)
            {
                Debug.LogWarning($"[Multilingual] RemoveLanguage 失败：找不到语种 {code}");
                return false;
            }

            if (Data.GetAllLanguages().Count <= 1)
            {
                Debug.LogWarning("[Multilingual] 不能移除最后一个可用语种");
                return false;
            }

            var wasCurrent = string.Equals(
                normalized, _currentLanguageCode, StringComparison.Ordinal);

            var id = Data.GetIdByCode(normalized);
            if (id < 0 || !Data.RemoveData(id))
                return false;

            if (deletePersisted)
                DeletePersistedLanguage(normalized);

            OnLanguageRemoved?.Invoke(normalized);

            if (wasCurrent)
            {
                // 玩家手动选择被移除后，清除保存值，下次启动重新自动选择。
                if (string.Equals(
                        PlayerPrefs.GetString(SaveKey, string.Empty),
                        normalized,
                        StringComparison.Ordinal))
                {
                    PlayerPrefs.DeleteKey(SaveKey);
                }

                var fallback = ResolveLanguage(_defaultLanguageCode);
                if (fallback == null)
                {
                    var all = Data.GetAllLanguages();
                    fallback = all.Count > 0 ? all[0].code : null;
                }

                if (!string.IsNullOrEmpty(fallback))
                    SetLanguageInternal(fallback, save: false, notify: true);
            }

            return true;
        }

        // ------------------------------------------------------------------
        // 初始化
        // ------------------------------------------------------------------

        private void EnsureInitialized()
        {
            if (_initialized)
                return;

            _initialized = true;

            LoadBuiltinLanguages();
            LoadCustomLanguages();

            var all = Data.GetAllLanguages();
            if (all.Count == 0)
            {
                Debug.LogError(
                    "[Multilingual] 没有任何语言包。请把 en.json 等文件放到 " +
                    "Resources/Multilingual 下，或调用 AddLanguageFromJson 添加。");
                return;
            }

            // 1. 玩家手动保存的选择
            var saved = PlayerPrefs.GetString(SaveKey, string.Empty);
            var selected = ResolveLanguage(saved);

            // 2. 没有手动选择：系统语言
            if (selected == null && AutoDetectSystemLanguage)
                selected = ResolveLanguage(MapSystemLanguage(Application.systemLanguage));

            // 3. 默认语种
            if (selected == null)
                selected = ResolveLanguage(_defaultLanguageCode);

            // 4. 第一个可用语种
            if (selected == null)
                selected = all[0].code;

            // 自动选择不写 PlayerPrefs，这样下次启动还能跟随系统/新增语言包。
            SetLanguageInternal(selected, save: false, notify: false);
        }

        private void LoadBuiltinLanguages()
        {
            var assets = Resources.LoadAll<TextAsset>(BuiltinResourcesFolder);
            foreach (var asset in assets)
            {
                if (asset == null)
                    continue;

                if (!TryParseLanguageJson(asset.text, asset.name, out var data, out var error))
                {
                    Debug.LogWarning($"[Multilingual] 内置语言包解析失败 {asset.name}: {error}");
                    continue;
                }

                data.isBuiltin = true;
                data.displayName ??= data.code;
                Data.AddData(data);
            }
        }

        private void LoadCustomLanguages()
        {
            try
            {
                if (!Directory.Exists(CustomLanguageDirectory))
                    return;

                foreach (var file in Directory.GetFiles(CustomLanguageDirectory, "*.json"))
                {
                    string json;
                    try
                    {
                        json = File.ReadAllText(file, Encoding.UTF8);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[Multilingual] 读取自定义语言包失败 {file}: {e.Message}");
                        continue;
                    }

                    var fallbackCode = Path.GetFileNameWithoutExtension(file);
                    if (!TryParseLanguageJson(json, fallbackCode, out var data, out var error))
                    {
                        Debug.LogWarning($"[Multilingual] 自定义语言包解析失败 {file}: {error}");
                        continue;
                    }

                    data.isBuiltin = false;
                    data.displayName ??= Data.GetByCode(data.code)?.displayName ?? data.code;

                    // 自定义文件里的条目都算自定义覆盖，后续持久化时保留。
                    foreach (var key in data.entries.Keys)
                        data.customKeys.Add(key);

                    Data.AddData(data);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Multilingual] 扫描自定义语言包失败: {e.Message}");
            }
        }

        // ------------------------------------------------------------------
        // 语种解析
        // ------------------------------------------------------------------

        private string ResolveLanguage(string code)
        {
            var normalized = NormalizeCode(code);
            if (string.IsNullOrEmpty(normalized))
                return null;

            if (Data.HasLanguage(normalized))
                return normalized;

            // en-US → en、pt-BR → pt 的退路
            var dash = normalized.IndexOf('-');
            if (dash > 0)
            {
                var baseCode = normalized.Substring(0, dash);
                if (Data.HasLanguage(baseCode))
                    return baseCode;
            }

            return null;
        }

        private bool SetLanguageInternal(string code, bool save, bool notify)
        {
            var resolved = ResolveLanguage(code);
            if (resolved == null)
            {
                Debug.LogWarning($"[Multilingual] 语种不存在或不可用: {code}");
                return false;
            }

            if (string.Equals(resolved, _currentLanguageCode, StringComparison.Ordinal))
            {
                if (save)
                    SaveLanguage();

                return true;
            }

            var oldCode = _currentLanguageCode;
            _currentLanguageCode = resolved;

            if (save)
                SaveLanguage();

            if (notify)
                OnLanguageChanged?.Invoke(oldCode, resolved);

            return true;
        }

        private void SaveLanguage()
        {
            if (string.IsNullOrEmpty(_currentLanguageCode))
                return;

            PlayerPrefs.SetString(SaveKey, _currentLanguageCode);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 当前语种的条目被覆盖时，code 没有变化，但文案已经变了。
        /// 这里用 (code, code) 触发一次 OnLanguageChanged，让视图重新取文本。
        /// </summary>
        private void NotifyIfCurrentLanguageUpdated(string code)
        {
            if (string.Equals(code, _currentLanguageCode, StringComparison.Ordinal))
                OnLanguageChanged?.Invoke(code, code);
        }

        /// <summary>
        /// 统一语种代码：下划线转连字符，语言小写、地区大写。
        /// 中文做特殊归一：zh / zh-Hans / zh-CN / zh-SG → zh-CN；
        /// zh-Hant / zh-TW / zh-HK / zh-MO → zh-TW。
        /// </summary>
        public static string NormalizeCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return null;

            code = code.Trim().Replace('_', '-');

            // code 会被用作持久化文件名，拒绝路径分隔符和 ..
            if (code.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || code.Contains(".."))
                return null;

            var lower = code.ToLowerInvariant();

            if (lower == "zh"
                || lower.StartsWith("zh-hans")
                || lower.StartsWith("zh-cn")
                || lower.StartsWith("zh-sg"))
            {
                return "zh-CN";
            }

            if (lower.StartsWith("zh-hant")
                || lower.StartsWith("zh-tw")
                || lower.StartsWith("zh-hk")
                || lower.StartsWith("zh-mo"))
            {
                return "zh-TW";
            }

            var parts = code.Split('-');
            if (parts.Length == 1)
                return parts[0].ToLowerInvariant();

            return parts[0].ToLowerInvariant() + "-" + parts[1].ToUpperInvariant();
        }

        private static string MapSystemLanguage(SystemLanguage language)
        {
            switch (language)
            {
                case SystemLanguage.ChineseSimplified:
                case SystemLanguage.Chinese:
                    return "zh-CN";
                case SystemLanguage.ChineseTraditional:
                    return "zh-TW";
                case SystemLanguage.English:
                    return "en";
                case SystemLanguage.Japanese:
                    return "ja";
                case SystemLanguage.Korean:
                    return "ko";
                case SystemLanguage.French:
                    return "fr";
                case SystemLanguage.German:
                    return "de";
                case SystemLanguage.Spanish:
                    return "es";
                case SystemLanguage.Portuguese:
                    return "pt";
                case SystemLanguage.Russian:
                    return "ru";
                case SystemLanguage.Italian:
                    return "it";
                case SystemLanguage.Polish:
                    return "pl";
                case SystemLanguage.Turkish:
                    return "tr";
                case SystemLanguage.Arabic:
                    return "ar";
                case SystemLanguage.Thai:
                    return "th";
                case SystemLanguage.Vietnamese:
                    return "vi";
                case SystemLanguage.Indonesian:
                    return "id";
                case SystemLanguage.Dutch:
                    return "nl";
                case SystemLanguage.Swedish:
                    return "sv";
                case SystemLanguage.Danish:
                    return "da";
                case SystemLanguage.Norwegian:
                    return "no";
                case SystemLanguage.Finnish:
                    return "fi";
                case SystemLanguage.Czech:
                    return "cs";
                case SystemLanguage.Greek:
                    return "el";
                case SystemLanguage.Hungarian:
                    return "hu";
                case SystemLanguage.Romanian:
                    return "ro";
                case SystemLanguage.Bulgarian:
                    return "bg";
                case SystemLanguage.Ukrainian:
                    return "uk";
                case SystemLanguage.Hebrew:
                    return "he";
                case SystemLanguage.Hindi:
                    return "hi";
                default:
                    return null;
            }
        }

        // ------------------------------------------------------------------
        // 文本查询
        // ------------------------------------------------------------------

        private bool TryGetText(string languageCode, string key, out string text)
        {
            text = null;
            if (string.IsNullOrEmpty(languageCode))
                return false;

            var data = Data.GetByCode(languageCode);
            return data?.entries != null && data.entries.TryGetValue(key, out text);
        }

        private void WarnMissingKeyOnce(string key)
        {
            if (_warnedMissingKeys.Add(key))
                Debug.LogWarning($"[Multilingual] 缺少文本 key: {key}");
        }

        // ------------------------------------------------------------------
        // JSON 与持久化
        // ------------------------------------------------------------------

        private static string CustomLanguageDirectory
            => Path.Combine(Application.persistentDataPath, CustomFolderName);

        private static string GetCustomPath(string code)
            => Path.Combine(CustomLanguageDirectory, code + ".json");

        private static bool TryParseLanguageJson(
            string json,
            string fallbackCode,
            out LanguageData data,
            out string error)
        {
            data = null;
            error = null;

            if (string.IsNullOrWhiteSpace(json))
            {
                error = "JSON 为空";
                return false;
            }

            // 兼容带 BOM 的文件
            json = json.TrimStart((char)0xFEFF);

            LanguagePackJson pack;
            try
            {
                pack = JsonUtility.FromJson<LanguagePackJson>(json);
            }
            catch (Exception e)
            {
                error = e.Message;
                return false;
            }

            if (pack == null)
            {
                error = "JSON 结构无效";
                return false;
            }

            var code = NormalizeCode(
                string.IsNullOrWhiteSpace(pack.code) ? fallbackCode : pack.code);

            if (string.IsNullOrEmpty(code))
            {
                error = "缺少 code";
                return false;
            }

            data = new LanguageData
            {
                code = code,
                displayName = string.IsNullOrWhiteSpace(pack.displayName)
                    ? null
                    : pack.displayName.Trim()
            };

            if (pack.entries != null)
            {
                foreach (var entry in pack.entries)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.key))
                        continue;

                    data.entries[entry.key] = entry.value ?? string.Empty;
                }
            }

            return true;
        }

        private static bool PersistLanguage(LanguageData data)
        {
            if (data == null)
                return false;

            try
            {
                Directory.CreateDirectory(CustomLanguageDirectory);

                var entries = data.entries ?? new Dictionary<string, string>();
                var customKeys = data.customKeys ?? new HashSet<string>(StringComparer.Ordinal);
                var pack = new LanguagePackJson
                {
                    code = data.code,
                    displayName = data.displayName,
                    entries = new List<TranslationEntry>(customKeys.Count)
                };

                foreach (var pair in entries)
                {
                    if (!customKeys.Contains(pair.Key))
                        continue;

                    pack.entries.Add(new TranslationEntry
                    {
                        key = pair.Key,
                        value = pair.Value
                    });
                }

                var json = JsonUtility.ToJson(pack, true);
                File.WriteAllText(GetCustomPath(data.code), json, Utf8NoBom);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Multilingual] 保存语言包失败 {data.code}: {e}");
                return false;
            }
        }

        private static void DeletePersistedLanguage(string code)
        {
            try
            {
                var path = GetCustomPath(code);
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Multilingual] 删除自定义语言包失败 {code}: {e.Message}");
            }
        }
    }
}
