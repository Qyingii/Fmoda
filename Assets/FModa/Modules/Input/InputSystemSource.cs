using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FModa.InputSystem
{
    /// <summary>
    /// 输入读取核心（基于 Input System，无 Asset 依赖）。
    ///
    /// 每个 actionID 对应一个 InputAction，键盘/手柄 binding 挂在同一个 Action 下。
    ///
    /// 事件层严格对应 Input System 的原生三阶段：
    /// - Started   → InputAction.started    （按下开始）
    /// - Performed → InputAction.performed  （按下完成，Button 类型通常与 started 同帧）
    /// - Canceled  → InputAction.canceled   （抬起）
    ///
    /// 触点：
    /// - PressVirtual  触发 Started + Performed（瞬时按下，两个阶段合并）
    /// - ReleaseVirtual 触发 Canceled
    ///
    /// 没有每帧 Tick，没有 UpEnd 合成。
    /// </summary>
    public class InputSystemSource : IDisposable
    {
        /// <summary>actionID → InputAction</summary>
        private readonly Dictionary<string, InputAction> actions = new(StringComparer.Ordinal);

        // 三事件表（对应 started / performed / canceled）
        private readonly Dictionary<string, Action> startedHandlers   = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Action> performedHandlers = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Action> canceledHandlers  = new(StringComparer.Ordinal);

        /// <summary>触点虚拟键：actionID → 是否按住</summary>
        private readonly Dictionary<string, bool> virtualHeld = new(StringComparer.Ordinal);

        // ===============================================================
        // 构建
        // ===============================================================
        /// <summary>
        /// 根据绑定表重建所有 InputAction。
        /// 会销毁旧 Action，但保留订阅表，运行时重绑定后订阅关系不丢。
        /// </summary>
        public void Build(IReadOnlyList<KeyBinding> bindings)
        {
            // 销毁旧 Action
            foreach (var a in actions.Values)
            {
                if (a == null) continue;
                a.Disable();
                a.Dispose();
            }
            actions.Clear();

            for (int i = 0; i < bindings.Count; i++)
            {
                var b = bindings[i];
                if (b == null || string.IsNullOrEmpty(b.actionID)) continue;
                if (actions.ContainsKey(b.actionID)) continue;

                // Button 类型：天然支持 started / performed / canceled
                var action = new InputAction(b.actionID, InputActionType.Button);

                // 键盘主键
                string p1 = KeyCodeToPath.ToPath(b.primaryKey);
                if (p1 != null) action.AddBinding(p1);

                // 键盘备用键
                string p2 = KeyCodeToPath.ToPath(b.alternateKey);
                if (p2 != null) action.AddBinding(p2);

                // 手柄控件
                string gp = GamepadControlPath.ToPath(b.gamepadKey);
                if (gp != null) action.AddBinding(gp);

                // 严格映射三阶段
                string id = b.actionID;
                action.started   += _ => FireStarted(id);
                action.performed += _ => FirePerformed(id);
                action.canceled  += _ => FireCanceled(id);

                actions[id] = action;
                action.Enable();
            }
        }

        // ===============================================================
        // 事件分发
        // ===============================================================
        private void FireStarted(string id)
        {
            if (startedHandlers.TryGetValue(id, out var h)) h?.Invoke();
        }

        private void FirePerformed(string id)
        {
            if (performedHandlers.TryGetValue(id, out var h)) h?.Invoke();
        }

        private void FireCanceled(string id)
        {
            if (canceledHandlers.TryGetValue(id, out var h)) h?.Invoke();
        }

        // ===============================================================
        // 订阅
        // ===============================================================
        /// <summary>订阅“按下开始”（对应 started）。</summary>
        public void SubscribeStarted(string actionID, Action onStarted)
        {
            if (string.IsNullOrEmpty(actionID) || onStarted == null) return;
            startedHandlers.TryGetValue(actionID, out var h);
            startedHandlers[actionID] = h + onStarted;
        }

        /// <summary>订阅“按下完成”（对应 performed）。</summary>
        public void SubscribePerformed(string actionID, Action onPerformed)
        {
            if (string.IsNullOrEmpty(actionID) || onPerformed == null) return;
            performedHandlers.TryGetValue(actionID, out var h);
            performedHandlers[actionID] = h + onPerformed;
        }

        /// <summary>订阅“抬起”（对应 canceled）。</summary>
        public void SubscribeCanceled(string actionID, Action onCanceled)
        {
            if (string.IsNullOrEmpty(actionID) || onCanceled == null) return;
            canceledHandlers.TryGetValue(actionID, out var h);
            canceledHandlers[actionID] = h + onCanceled;
        }

        // ===============================================================
        // 取消订阅
        // ===============================================================
        public void UnsubscribeStarted(string actionID, Action onStarted)
        {
            if (string.IsNullOrEmpty(actionID) || onStarted == null) return;
            if (startedHandlers.TryGetValue(actionID, out var h))
                startedHandlers[actionID] = h - onStarted;
        }

        public void UnsubscribePerformed(string actionID, Action onPerformed)
        {
            if (string.IsNullOrEmpty(actionID) || onPerformed == null) return;
            if (performedHandlers.TryGetValue(actionID, out var h))
                performedHandlers[actionID] = h - onPerformed;
        }

        public void UnsubscribeCanceled(string actionID, Action onCanceled)
        {
            if (string.IsNullOrEmpty(actionID) || onCanceled == null) return;
            if (canceledHandlers.TryGetValue(actionID, out var h))
                canceledHandlers[actionID] = h - onCanceled;
        }

        // ===============================================================
        // 轮询
        // ===============================================================
        /// <summary>本帧是否按下（一次性）。</summary>
        public bool IsDown(string actionID)
        {
            if (string.IsNullOrEmpty(actionID)) return false;
            if (virtualHeld.TryGetValue(actionID, out var v) && v) return true;
            return actions.TryGetValue(actionID, out var a) && a.WasPressedThisFrame();
        }

        /// <summary>本帧是否按住（持续状态）。</summary>
        public bool IsHeld(string actionID)
        {
            if (string.IsNullOrEmpty(actionID)) return false;
            if (virtualHeld.TryGetValue(actionID, out var v) && v) return true;
            return actions.TryGetValue(actionID, out var a) && a.IsPressed();
        }

        /// <summary>本帧是否抬起（一次性）。</summary>
        public bool IsUp(string actionID)
        {
            if (string.IsNullOrEmpty(actionID)) return false;
            return actions.TryGetValue(actionID, out var a) && a.WasReleasedThisFrame();
        }

        // ===============================================================
        // 触点
        // ===============================================================
        /// <summary>
        /// 触点按下：维护 held 状态，触发 Started + Performed。
        /// 触点是瞬时按下，两个阶段合并触发。
        /// </summary>
        public void PressVirtual(string actionID)
        {
            if (string.IsNullOrEmpty(actionID)) return;
            if (virtualHeld.TryGetValue(actionID, out var v) && v) return;

            virtualHeld[actionID] = true;
            FireStarted(actionID);
            FirePerformed(actionID);
        }

        /// <summary>
        /// 触点抬起：清除 held 状态，触发 Canceled。
        /// </summary>
        public void ReleaseVirtual(string actionID)
        {
            if (string.IsNullOrEmpty(actionID)) return;
            if (!virtualHeld.TryGetValue(actionID, out var v) || !v) return;

            virtualHeld[actionID] = false;
            FireCanceled(actionID);
        }

        // ===============================================================
        /// <summary>
        /// 销毁所有 InputAction。不清空订阅表，以便重绑定后订阅保留。
        /// </summary>
        public void Dispose()
        {
            foreach (var a in actions.Values)
            {
                if (a == null) continue;
                a.Disable();
                a.Dispose();
            }
            actions.Clear();
            virtualHeld.Clear();
            // startedHandlers / performedHandlers / canceledHandlers 故意保留
        }
    }
}