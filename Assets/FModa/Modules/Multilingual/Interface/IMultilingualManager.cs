using System;
using System.Collections.Generic;
using UnityEngine;

namespace FModa.Multilingual
{
    /// <summary>
    /// 多语言模块的对外契约。视图只依赖本接口，不访问 DataCenter。
    /// 获取模块时仍使用具体类型：GF.GetModule&lt;MultilingualManager&gt;()。
    /// </summary>
    public interface IMultilingualManager
    {
        /// <summary>当前语种变化时触发：(旧 code, 新 code)。</summary>
        event Action<string, string> OnLanguageChanged;

        /// <summary>新增或更新某语种数据后触发：(code)。</summary>
        event Action<string> OnLanguageAdded;

        /// <summary>移除某语种后触发：(code)。</summary>
        event Action<string> OnLanguageRemoved;

        /// <summary>当前语种代码，例如 en、zh-CN。</summary>
        string CurrentLanguageCode { get; }

        /// <summary>找不到当前语种条目时使用的兜底语种，默认 en。</summary>
        string DefaultLanguageCode { get; set; }

        /// <summary>
        /// 没有保存的玩家手动选择时，是否按系统语言自动选择。
        /// 默认 true。必须在模块初始化前设置才有效，通常在 Flow 里改。
        /// </summary>
        bool AutoDetectSystemLanguage { get; set; }

        /// <summary>
        /// 由 Flow 在进入时调用：加载内置/自定义语言包并选择当前语种。
        /// 重复调用幂等；即使不调用，首次访问语言 API 也会懒初始化。
        /// </summary>
        void Init();

        /// <summary>获取当前可用语种，供设置界面生成列表。</summary>
        IReadOnlyList<LanguageInfo> GetAvailableLanguages();

        bool HasLanguage(string code);

        /// <summary>切换语种。成功返回 true，不存在返回 false。</summary>
        bool SetLanguage(string code);

        /// <summary>切换语种。成功返回 true，不存在返回 false。</summary>
        bool SetLanguage(SystemLanguage code);

        /// <summary>
        /// 取文本：当前语种 → 兜底语种 → key 本身。
        /// 缺失 key 只警告一次，避免刷屏。
        /// </summary>
        string GetText(string key);

        /// <summary>取文本并按 string.Format 填充参数。</summary>
        string GetText(string key, params object[] args);

        /// <summary>当前语种或兜底语种是否存在该 key。</summary>
        bool HasKey(string key);

        /// <summary>
        /// 添加或覆盖一个语种。
        /// persist=true 时写入 persistentDataPath/Multilingual/&lt;code&gt;.json，
        /// 下次启动自动加载。
        /// </summary>
        bool AddLanguage(
            string code,
            string displayName,
            IReadOnlyDictionary<string, string> entries,
            bool persist = false);

        /// <summary>从 JSON 文本添加语种，JSON 结构见 Doc/README.md。</summary>
        bool AddLanguageFromJson(string json, bool persist = false);

        /// <summary>
        /// 移除语种。内置语种只在本会话移除，重启后会重新加载；
        /// deletePersisted=true 会同时删除自定义 JSON 覆盖文件。
        /// 不允许移除最后一个可用语种。
        /// </summary>
        bool RemoveLanguage(string code, bool deletePersisted = false);
    }
}
