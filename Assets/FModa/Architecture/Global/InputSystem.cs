using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;

namespace FModa.InputSystem
{
    /// <summary>
    /// 统一输入管理：
    /// - Hold 类（移动等）：每帧通过 IsHeld() 查询
    /// - Down/Up 类（攻击、技能等）：通过 OnShortcutTriggered 事件分发
    /// - 支持运行时重绑定、JSON 存读、键位冲突检测、输入焦点开关
    /// </summary>
    public class InputManager
    {
        private Dictionary<string, KeyBinding> currentBindings =
            new Dictionary<string, KeyBinding>(StringComparer.Ordinal);

        // 缓存所有绑定，避免每帧遍历 Dictionary.Values 的枚举器开销
        private readonly List<KeyBinding> bindingCache = new List<KeyBinding>();

        private string configPath;

        /// <summary>
        /// 输入开关。打开 UI 输入框、暂停等场景设为 false。
        /// </summary>
        public bool InputEnabled { get; set; } = true;

        public InputManager()
        {
            Init();
        }

        private void Init()
        {
            configPath = Path.Combine(Application.persistentDataPath, "keybindings.json");

            LoadBindings();
        }

        // ---------------------------------------------------------------
        // 查询接口（供移动等 Hold 逻辑每帧调用）
        // ---------------------------------------------------------------

        /// <summary>
        /// 某动作本帧是否被触发。
        /// </summary>
        public bool Trigger(string actionID)
        {
            if (!InputEnabled) return false;
            if (!currentBindings.TryGetValue(actionID, out var binding))
            {
                Debug.LogWarning($"未找到动作 {actionID} 的绑定。");
                return false;
            }
            return binding.IsTriggered();
        }

        // ---------------------------------------------------------------
        // 初始化 / 默认键位
        // ---------------------------------------------------------------

        /// <summary>
        /// 初始化 / 默认键位，这里添加你需要的输入事件。
        /// </summary>
        private void InitializeDefaultBindings()
        {
            var list = new List<KeyBinding>
            {
                // 单位控制
                new KeyBinding("UnitDeductionOfBlood",KeyCode.Q, KeyBinding.KeyBindingType.Down),
                new KeyBinding("MoveUp",KeyCode.W, KeyBinding.KeyBindingType.Hold),
                new KeyBinding("OpenUI",KeyCode.E, KeyBinding.KeyBindingType.Down),
                new KeyBinding("Spawn",KeyCode.R, KeyBinding.KeyBindingType.Down),

                // 结束流程
                new KeyBinding("EndFlow",KeyCode.Space, KeyBinding.KeyBindingType.Down),
            };

            RebuildBindings(list);
        }

        // ---------------------------------------------------------------
        // 重绑定 / 冲突检测
        // ---------------------------------------------------------------

        /// <summary>
        /// 重绑定某动作的按键。返回是否成功。
        /// </summary>
        public bool Rebind(string actionID, KeyCode newPrimary,
            KeyCode newAlternate = KeyCode.None,
            KeyCode[] newModifiers = null,
            bool allowConflict = false)
        {
            if (!currentBindings.TryGetValue(actionID, out var binding))
                return false;

            if (!allowConflict)
            {
                string conflict = FindConflict(actionID, newPrimary, newAlternate, newModifiers);
                if (conflict != null)
                {
                    Debug.LogWarning($"[InputManager] 键位冲突：{actionID} 与 {conflict} 使用了相同按键");
                    return false;
                }
            }

            binding.primaryKey = newPrimary;
            binding.alternateKey = newAlternate;
            binding.modifierKeys = newModifiers ?? Array.Empty<KeyCode>();
            return true;
        }

        /// <summary>
        /// 查找冲突动作（返回冲突的 actionID，无冲突返回 null）。
        /// 简单策略：主键相同即视为冲突（未细分修饰键组合）。
        /// </summary>
        public string FindConflict(string excludeActionID, KeyCode primary,
            KeyCode alternate, KeyCode[] modifiers)
        {
            foreach (var kv in currentBindings)
            {
                if (kv.Key == excludeActionID) continue;
                var other = kv.Value;
                if (other.UsesKey(primary) || other.UsesKey(alternate))
                    return kv.Key;
            }
            return null;
        }

        // ---------------------------------------------------------------
        // 存 / 读 JSON
        // ---------------------------------------------------------------

        [Serializable]
        private class BindingListWrapper
        {
            public List<KeyBinding> bindings;
        }

        public void SaveBindings()
        {
            try
            {
                var wrapper = new BindingListWrapper { bindings = bindingCache };
                string json = JsonUtility.ToJson(wrapper, true);
                File.WriteAllText(configPath, json);
                Debug.Log($"[InputManager] 键位已保存到 {configPath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[InputManager] 保存键位失败: {e}");
            }
        }

        public bool LoadBindings()
        {
            try
            {
                if (!File.Exists(configPath))
                {
                    InitializeDefaultBindings(); // 默认键位
                    return false;
                }

                string json = File.ReadAllText(configPath);
                var wrapper = JsonUtility.FromJson<BindingListWrapper>(json);
                if (wrapper?.bindings == null || wrapper.bindings.Count == 0)
                    return false;

                RebuildBindings(wrapper.bindings);
                Debug.Log($"[InputManager] 已从 {configPath} 加载键位");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[InputManager] 加载键位失败: {e}");
                return false;
            }
        }

        public void ResetToDefault()
        {
            InitializeDefaultBindings();
        }

        // ---------------------------------------------------------------
        // 内部：重建字典与缓存
        // ---------------------------------------------------------------

        private void RebuildBindings(List<KeyBinding> source)
        {
            currentBindings.Clear();
            bindingCache.Clear();

            for (int i = 0; i < source.Count; i++)
            {
                KeyBinding b = source[i];
                if (b == null || string.IsNullOrEmpty(b.actionID))
                {
                    Debug.LogWarning($"[InputManager] 第 {i} 个绑定缺少 actionID，已跳过");
                    continue;
                }

                if (currentBindings.ContainsKey(b.actionID))
                {
                    Debug.LogWarning($"[InputManager] 重复 actionID: {b.actionID}，已跳过");
                    continue;
                }

                // 兜底修饰键，避免运行期 NRE
                if (b.modifierKeys == null)
                    b.modifierKeys = Array.Empty<KeyCode>();

                currentBindings.Add(b.actionID, b);
                bindingCache.Add(b);
            }
        }

        // ---------------------------------------------------------------
        // 可选：调试输出当前所有键位
        // ---------------------------------------------------------------

        [ContextMenu("打印当前键位")]
        public void DebugPrintBindings()
        {
            foreach (var kv in currentBindings)
            {
                var b = kv.Value;
                Debug.Log($"{b.displayName} [{b.actionID}] : {b.primaryKey}" +
                          (b.alternateKey != KeyCode.None ? $" / {b.alternateKey}" : "") +
                          $" ({b.useType})");
            }
        }
    }
}