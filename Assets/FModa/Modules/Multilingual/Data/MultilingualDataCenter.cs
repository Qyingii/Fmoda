using System;
using System.Collections.Generic;
using System.IO;
using FModa.Data;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace FModa.Multilingual
{
    /// <summary>
    /// 多语言数据中心。除基类的 id 索引外，额外维护「语种代码 → id」，
    /// 这样模块可以通过 en / zh-CN 这样的代码直接取数据，而不用关心内部 id。
    ///
    /// 同一个 code 重复添加时执行合并：新条目覆盖旧条目，未提供的条目保留。
    /// 这样「内置语言包 + 玩家自定义覆盖包」可以按加载顺序叠加。
    /// </summary>
    public class MultilingualDataCenter : DataCenter<LanguageData>, IDataCenter
    {

        public MultilingualDataCenter(int centerCode) : base(centerCode) { }

        private readonly Dictionary<string, long> _codeToId =
            new(StringComparer.OrdinalIgnoreCase);

        public override long AddData(LanguageData data)
        {
            if (data == null || string.IsNullOrWhiteSpace(data.code))
            {
                Debug.LogWarning("[Multilingual] 无法添加 code 为空的语种数据");
                return -1;
            }

            if (string.IsNullOrWhiteSpace(data.displayName))
                data.displayName = data.code;

            data.entries ??= new Dictionary<string, string>(StringComparer.Ordinal);
            data.customKeys ??= new HashSet<string>(StringComparer.Ordinal);

            if (_codeToId.TryGetValue(data.code, out var existingId))
            {
                var existing = datas[existingId];
                existing.entries ??= new Dictionary<string, string>(StringComparer.Ordinal);

                foreach (var pair in data.entries)
                    existing.entries[pair.Key] = pair.Value;

                existing.customKeys ??= new HashSet<string>(StringComparer.Ordinal);
                existing.customKeys.UnionWith(data.customKeys);

                if (!string.IsNullOrWhiteSpace(data.displayName))
                    existing.displayName = data.displayName;

                // 内置包与自定义覆盖包合并后，仍视为内置可用语种；
                // 要彻底移除自定义覆盖，调用 RemoveLanguage(code, deletePersisted: true)。
                existing.isBuiltin |= data.isBuiltin;
                return existingId;
            }

            var id = base.AddData(data);
            if (id >= 0)
                _codeToId[data.code] = id;

            return id;
        }

        public override bool RemoveData(long Id)
        {
            if (datas.TryGetValue(Id, out var data) && data != null)
                _codeToId.Remove(data.code);

            return base.RemoveData(Id);
        }

        public override void Clear()
        {
            _codeToId.Clear();
            base.Clear();
        }

        /// <summary>通用存档需要保留 customKeys，否则后续语言包持久化会丢失自定义条目标记。</summary>
        protected override JObject SerializeData(LanguageData data)
        {
            var json = base.SerializeData(data);
            var customKeys = new List<string>(data.customKeys ?? new HashSet<string>());
            customKeys.Sort(StringComparer.Ordinal);
            json["customKeys"] = new JArray(customKeys);
            return json;
        }

        protected override LanguageData DeserializeData(JObject json)
        {
            var data = base.DeserializeData(json);
            data.entries = data.entries == null
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : new Dictionary<string, string>(data.entries, StringComparer.Ordinal);

            var customKeys = json["customKeys"]?.ToObject<List<string>>() ?? new List<string>();
            data.customKeys = new HashSet<string>(customKeys, StringComparer.Ordinal);
            return data;
        }

        protected override void ValidateSaveData(LanguageData data)
        {
            if (data == null || string.IsNullOrWhiteSpace(data.code))
                throw new InvalidDataException("多语言存档包含 code 为空的语种数据");
        }

        protected override void ValidateAppendData(LanguageData data)
        {
            ValidateSaveData(data);
            if (_codeToId.ContainsKey(data.code))
                throw new InvalidDataException(
                    $"语种 {data.code} 已存在；追加导入不允许通过 AddData 静默合并语种");
        }

        protected override void RebuildAfterRestore()
        {
            _codeToId.Clear();
            foreach (var pair in datas)
            {
                string code = pair.Value.code;
                if (_codeToId.ContainsKey(code))
                    throw new InvalidDataException(
                        $"多语言存档包含大小写不敏感规则下重复的语种代码：{code}");
                _codeToId.Add(code, pair.Key);
            }
        }

        public LanguageData GetByCode(string code)
        {
            if (string.IsNullOrEmpty(code))
                return null;

            return _codeToId.TryGetValue(code, out var id)
                && datas.TryGetValue(id, out var data)
                    ? data
                    : null;
        }

        public bool HasLanguage(string code) => GetByCode(code) != null;

        public long GetIdByCode(string code)
            => !string.IsNullOrEmpty(code)
                && _codeToId.TryGetValue(code, out var id)
                && datas.ContainsKey(id)
                    ? id
                    : -1;

        /// <summary>返回按 code 排序的快照，保证「取第一个可用语种」时结果稳定。</summary>
        public IReadOnlyList<LanguageData> GetAllLanguages()
        {
            var list = new List<LanguageData>(datas.Values);
            list.Sort((a, b) =>
                string.Compare(a.code, b.code, StringComparison.OrdinalIgnoreCase));
            return list;
        }
    }
}
