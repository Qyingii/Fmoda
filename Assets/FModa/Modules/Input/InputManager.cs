using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;

namespace FModa.InputSystem
{
    /// <summary>
    /// 输入管理器（基于 Input System，无 Asset 依赖）。
    ///
    /// 事件三件：Started / Performed / Canceled，严格对应 Input System 原生阶段。
    /// 没有 Tick，没有每帧合成。
    ///
    /// Hold 语义由 Started + Canceled 组合表达，或直接使用轮询 TriggerHold。
    /// </summary>
    public class InputManager : IModule, IInputManager
    {
        private string configPath;
        private readonly Dictionary<string, KeyBinding> currentBindings =
            new(StringComparer.Ordinal);
        private readonly List<KeyBinding> bindingCache = new();

        private InputSystemSource source;
        public InputSystemSource Source => source;

        public bool InputEnabled { get; set; } = true;
        public Type InterfaceType => typeof(IInputManager);
        public int Priority => 5;

        public void OnAwake() => Init();

        private void Init()
        {
            configPath = Path.Combine(Application.persistentDataPath, "keybindings.json");
            source = new InputSystemSource();
            LoadBindings();
        }

        // ===============================================================
        // 轮询
        // ===============================================================
        /// <summary>本帧某动作是否按下（一次性）。</summary>
        public bool KeyDown(string actionID)
        {
            if (!InputEnabled) return false;
            return source.IsDown(actionID);
        }

        /// <summary>本帧某动作是否按住（持续状态）。</summary>
        public bool KeyHold(string actionID)
        {
            if (!InputEnabled) return false;
            return source.IsHeld(actionID);
        }

        /// <summary>本帧某动作是否抬起（一次性）。</summary>
        public bool KeyUp(string actionID)
        {
            if (!InputEnabled) return false;
            return source.IsUp(actionID);
        }

        // ===============================================================
        // 事件（严格三事件）
        // ===============================================================
        /// <summary>订阅“按下开始”。</summary>
        public void SubscribeStarted(string actionID, Action onStarted) => source.SubscribeStarted(actionID, onStarted);

        /// <summary>订阅“按下完成”。</summary>
        public void SubscribePerformed(string actionID, Action onPerformed) => source.SubscribePerformed(actionID, onPerformed);

        /// <summary>订阅“抬起”。</summary>
        public void SubscribeCanceled(string actionID, Action onCanceled) => source.SubscribeCanceled(actionID, onCanceled);

        /// <summary>取消“按下开始”订阅。</summary>
        public void UnsubscribeStarted(string actionID, Action onStarted) => source.UnsubscribeStarted(actionID, onStarted);

        /// <summary>取消“按下完成”订阅。</summary>
        public void UnsubscribePerformed(string actionID, Action onPerformed) => source.UnsubscribePerformed(actionID, onPerformed);

        /// <summary>取消“抬起”订阅。</summary>
        public void UnsubscribeCanceled(string actionID, Action onCanceled) => source.UnsubscribeCanceled(actionID, onCanceled);

        // ===============================================================
        // 触点
        // ===============================================================
        /// <summary>触点按下（由虚拟按钮调用）。</summary>
        public void PressVirtual(string actionID) => source.PressVirtual(actionID);

        /// <summary>触点抬起（由虚拟按钮调用）。</summary>
        public void ReleaseVirtual(string actionID) => source.ReleaseVirtual(actionID);

        // ===============================================================
        // 默认绑定
        // ===============================================================
        private void InitializeDefaultBindings()
        {
            RebuildBindings(InputInit.InitInputSystem());
        }

        // ===============================================================
        // 重绑定
        // ===============================================================
        public bool Rebind(string actionID, KeyCode newPrimary,
            KeyCode newAlternate = KeyCode.None,
            KeyCode[] newModifiers = null,
            bool allowConflict = false)
        {
            if (!currentBindings.TryGetValue(actionID, out var binding)) return false;

            if (!allowConflict)
            {
                string conflict = FindConflict(actionID, newPrimary, newAlternate, newModifiers);
                if (conflict != null)
                {
                    Debug.LogWarning($"[InputManager] 键盘键冲突：{actionID} 与 {conflict}");
                    return false;
                }
            }

            binding.primaryKey = newPrimary;
            binding.alternateKey = newAlternate;
            binding.modifierKeys = newModifiers ?? Array.Empty<KeyCode>();
            source.Build(bindingCache);
            return true;
        }

        public bool RebindGamepad(string actionID, GamepadCode newControl, bool allowConflict = false)
        {
            if (!currentBindings.TryGetValue(actionID, out var binding)) return false;

            if (!allowConflict)
            {
                string conflict = FindGamepadConflict(actionID, newControl);
                if (conflict != null)
                {
                    Debug.LogWarning($"[InputManager] 手柄键冲突：{actionID} 与 {conflict}");
                    return false;
                }
            }
            binding.gamepadKey = newControl;
            source.Build(bindingCache);
            return true;
        }

        // ===============================================================
        // 冲突检测
        // ===============================================================
        public string FindConflict(string excludeActionID, KeyCode primary,
            KeyCode alternate, KeyCode[] modifiers)
        {
            foreach (var kv in currentBindings)
            {
                if (kv.Key == excludeActionID) continue;
                if (kv.Value.UsesKey(primary) || kv.Value.UsesKey(alternate))
                    return kv.Key;
            }
            return null;
        }

        public string FindGamepadConflict(string excludeActionID, GamepadCode c)
        {
            if (c == GamepadCode.None) return null;
            foreach (var kv in currentBindings)
            {
                if (kv.Key == excludeActionID) continue;
                if (kv.Value.UsesGamepadKey(c)) return kv.Key;
            }
            return null;
        }

        // ===============================================================
        // 存 / 读
        // ===============================================================
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
                    InitializeDefaultBindings();
                    return false;
                }

                string json = File.ReadAllText(configPath);
                var wrapper = JsonUtility.FromJson<BindingListWrapper>(json);
                if (wrapper?.bindings == null || wrapper.bindings.Count == 0)
                {
                    InitializeDefaultBindings();
                    return false;
                }

                RebuildBindings(wrapper.bindings);
                Debug.Log($"[InputManager] 已从 {configPath} 加载键位");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[InputManager] 加载键位失败: {e}");
                InitializeDefaultBindings();
                return false;
            }
        }

        public void ResetToDefault()
        {
            InitializeDefaultBindings();
        }

        // ===============================================================
        // 内部：重建
        // ===============================================================
        private void RebuildBindings(List<KeyBinding> src)
        {
            currentBindings.Clear();
            bindingCache.Clear();

            for (int i = 0; i < src.Count; i++)
            {
                var b = src[i];
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
                if (b.modifierKeys == null)
                    b.modifierKeys = Array.Empty<KeyCode>();

                currentBindings.Add(b.actionID, b);
                bindingCache.Add(b);
            }

            source.Build(bindingCache);
        }

        // ===============================================================
        // 调试
        // ===============================================================
        [ContextMenu("打印当前键位")]
        public void DebugPrintBindings()
        {
            foreach (var kv in currentBindings)
            {
                var b = kv.Value;
                Debug.Log($"{b.displayName} [{b.actionID}] : K={b.primaryKey}" +
                          (b.alternateKey != KeyCode.None ? $" / {b.alternateKey}" : "") +
                          $", GP={GamepadControlPath.DisplayName(b.gamepadKey)}");
            }
        }
    }
}