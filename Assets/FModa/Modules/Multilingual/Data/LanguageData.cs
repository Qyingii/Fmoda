using System;
using System.Collections.Generic;

namespace FModa.Multilingual
{
    /// <summary>
    /// 一份语言的运行时数据。entries 的 key 是文本键，value 是对应翻译。
    /// 构建后由 <see cref="MultilingualDataCenter"/> 持有，模块只读使用。
    /// </summary>
    [Serializable]
    public class LanguageData : IData
    {
        /// <summary>标准化后的语种代码，例如 en、zh-CN、zh-TW、ja。</summary>
        public string code;

        /// <summary>给玩家显示的语种名称，例如 English、简体中文。</summary>
        public string displayName;

        /// <summary>是否来自 Resources 内置语言包。</summary>
        public bool isBuiltin;

        /// <summary>文本键 → 翻译。运行时不由视图直接修改。</summary>
        public Dictionary<string, string> entries = new(StringComparer.Ordinal);

        /// <summary>
        /// 哪些 key 来自自定义包/运行时添加。持久化时只写这些 key，
        /// 避免把内置语言包的内容复制进玩家存档。
        /// </summary>
        [NonSerialized]
        public HashSet<string> customKeys = new(StringComparer.Ordinal);
    }

    /// <summary>
    /// 语言包的 JSON 结构。JsonUtility 不支持 Dictionary，
    /// 所以文件里用 List&lt;TranslationEntry&gt; 表达键值对。
    /// </summary>
    [Serializable]
    public class LanguagePackJson
    {
        public string code;
        public string displayName;
        public List<TranslationEntry> entries = new();
    }

    [Serializable]
    public class TranslationEntry
    {
        public string key;
        public string value;
    }

    /// <summary>对外暴露的语种信息，视图用它在设置界面生成语言列表。</summary>
    public readonly struct LanguageInfo
    {
        public readonly string code;
        public readonly string displayName;
        public readonly bool isBuiltin;

        public LanguageInfo(string code, string displayName, bool isBuiltin)
        {
            this.code = code;
            this.displayName = displayName;
            this.isBuiltin = isBuiltin;
        }

        public override string ToString() => $"{displayName} ({code})";
    }
}
