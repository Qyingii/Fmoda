using System;

namespace FModa.InputSystem
{
    /// <summary>
    /// 输入管理器统一接口。
    ///
    /// 提供三层访问方式：
    /// 1. 轮询（TriggerDown / TriggerHold / TriggerUp）——业务层每帧主动查询；
    /// 2. 事件（严格对应 Input System 原生三阶段）——输入发生时回调，无输入时零开销；
    /// 3. 触点（PressVirtual / ReleaseVirtual）——由 UI 虚拟按钮在按下/抬起时调用。
    ///
    /// 事件层严格对应 InputAction 的三个原生阶段：
    /// - Started：按下开始（控件被 actuate）；
    /// - Performed：按下完成（超过阈值，Button 类型通常与 Started 同帧）；
    /// - Canceled：抬起（释放，或未达阈值就释放）。
    ///
    /// 注意：
    /// - Hold 语义由 Started + Canceled 组合表达（开始按住 / 松开），
    ///   或直接使用轮询 TriggerHold；不需要每帧 Hold 回调；
    /// - 轮询与事件可同时使用，互不干扰。
    /// </summary>
    public interface IInputManager
    {
        /// <summary>
        /// 输入开关。打开 UI 输入框、暂停、过场等场景设为 false。
        /// 为 false 时，所有轮询接口恒返回 false，事件不会触发。
        /// </summary>
        bool InputEnabled { get; set; }

        /// <summary>
        /// 获取某动作的绑定配置。
        /// </summary>
        /// <param name="actionID"></param>
        /// <returns></returns>
        KeyBinding GetBinding(string actionID);

        // ===============================================================
        // 轮询
        // ===============================================================

        /// <summary>
        /// 本帧某动作是否按下（一次性）。
        /// 适合 Down 类动作的轮询场景；同一帧内多次调用均返回 true。
        /// </summary>
        /// <param name="actionID">动作标识，例如 "EndFlow"</param>
        bool KeyDown(string actionID);

        /// <summary>
        /// 本帧某动作是否按住（持续状态）。
        /// 适合 Hold 类动作的轮询场景，例如移动、蓄力。
        /// </summary>
        /// <param name="actionID">动作标识，例如 "MoveUp"</param>
        bool KeyHold(string actionID);

        /// <summary>
        /// 本帧某动作是否抬起（一次性）。
        /// 适合 Up 类动作的轮询场景。
        /// </summary>
        /// <param name="actionID">动作标识，例如 "ReleaseSkill"</param>
        bool KeyUp(string actionID);

        // ===============================================================
        // 事件：订阅（三事件，对应 started / performed / canceled）
        // ===============================================================

        /// <summary>
        /// 订阅某动作的“按下开始”事件（对应 InputAction.started）。
        /// 控件被 actuate 时触发一次。
        /// </summary>
        /// <param name="actionID">动作标识</param>
        /// <param name="onStarted">回调</param>
        void SubscribeStarted(string actionID, Action onStarted);

        /// <summary>
        /// 订阅某动作的“按下完成”事件（对应 InputAction.performed）。
        /// 超过按下阈值时触发；Button 类型通常与 Started 同帧。
        /// </summary>
        /// <param name="actionID">动作标识</param>
        /// <param name="onPerformed">回调</param>
        void SubscribePerformed(string actionID, Action onPerformed);

        /// <summary>
        /// 订阅某动作的“抬起”事件（对应 InputAction.canceled）。
        /// 释放时触发一次。
        /// </summary>
        /// <param name="actionID">动作标识</param>
        /// <param name="onCanceled">回调</param>
        void SubscribeCanceled(string actionID, Action onCanceled);

        // ===============================================================
        // 事件：取消订阅
        // ===============================================================

        /// <summary>取消“按下开始”订阅。</summary>
        /// <param name="actionID">动作标识</param>
        /// <param name="onStarted">之前注册的回调</param>
        void UnsubscribeStarted(string actionID, Action onStarted);

        /// <summary>取消“按下完成”订阅。</summary>
        /// <param name="actionID">动作标识</param>
        /// <param name="onPerformed">之前注册的回调</param>
        void UnsubscribePerformed(string actionID, Action onPerformed);

        /// <summary>取消“抬起”订阅。</summary>
        /// <param name="actionID">动作标识</param>
        /// <param name="onCanceled">之前注册的回调</param>
        void UnsubscribeCanceled(string actionID, Action onCanceled);

        // ===============================================================
        // 触点
        // ===============================================================

        /// <summary>
        /// 触点按下。由 UI 虚拟按钮在 OnPointerDown 时调用。
        /// 会维护 held 状态，并触发 Started + Performed 事件。
        /// </summary>
        /// <param name="actionID">动作标识</param>
        void PressVirtual(string actionID);

        /// <summary>
        /// 触点抬起。由 UI 虚拟按钮在 OnPointerUp / OnPointerExit 时调用。
        /// 会清除 held 状态，并触发 Canceled 事件。
        /// </summary>
        /// <param name="actionID">动作标识</param>
        void ReleaseVirtual(string actionID);

        // ===============================================================
        // 持久化
        // ===============================================================

        /// <summary>
        /// 保存当前键位到硬盘（JSON）。
        /// 路径为 Application.persistentDataPath/keybindings.json。
        /// </summary>
        void SaveBindings();

        /// <summary>
        /// 从硬盘加载键位。
        /// 找不到文件或解析失败时，回退到默认键位（InputInit）。
        /// </summary>
        /// <returns>成功加载返回 true；回退默认返回 false</returns>
        bool LoadBindings();

        /// <summary>
        /// 重置键位到默认值（来自 InputInit）。
        /// 不会自动保存到硬盘，如需持久化请随后调用 SaveBindings()。
        /// </summary>
        void ResetToDefault();

        /// <summary>
        /// 调试：打印当前所有键位到 Console。
        /// </summary>
        void DebugPrintBindings();
    }
}