using System.Collections.Generic;
using UnityEngine;

namespace FModa.Modding
{
    /// <summary>
    /// Lua 模组可见的 API 沙箱。
    /// 每个模组拥有独立的 ModAPI 实例，数据完全隔离。
    ///
    /// 所有 public 方法即为 Lua 侧可调用的 API。
    /// </summary>
    public class ModAPI
    {
        private readonly ModManifest _manifest;
        private readonly Dictionary<string, object> _dataStore = new();

        /// <summary> 模组清单信息（Lua 侧可读取，如 api.Info.id）</summary>
        public ModManifest Info => _manifest;

        public ModAPI(ModManifest manifest)
        {
            _manifest = manifest;
        }

        // ── 日志 ──

        public void Log(string message)
        {
            Debug.Log($"[{_manifest.id}] {message}");
        }

        public void LogWarning(string message)
        {
            Debug.LogWarning($"[{_manifest.id}] {message}");
        }

        public void LogError(string message)
        {
            Debug.LogError($"[{_manifest.id}] {message}");
        }

        // ── 数据存储（每模组独立，完全隔离）──

        /// <summary> 存储数据 </summary>
        public void DataSet(string key, object value)
        {
            _dataStore[key] = value;
        }

        /// <summary> 读取数据（不存在时返回 null）</summary>
        public object DataGet(string key)
        {
            _dataStore.TryGetValue(key, out var value);
            return value;
        }

        /// <summary> 删除指定键 </summary>
        public void DataRemove(string key)
        {
            _dataStore.Remove(key);
        }

        /// <summary> 清空所有数据 </summary>
        public void DataClear()
        {
            _dataStore.Clear();
        }

        /// <summary> 检查键是否存在 </summary>
        public bool DataHas(string key)
        {
            return _dataStore.ContainsKey(key);
        }
    }
}
