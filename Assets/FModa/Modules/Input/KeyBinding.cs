using UnityEngine;
using System;

namespace FModa.InputSystem
{
    /// <summary>
    /// 单个按键绑定数据。纯数据类，可序列化到 JSON。
    /// 一个 actionID 同时绑定键盘主键、键盘备用键、手柄控件。
    /// </summary>
    [Serializable]
    public class KeyBinding
    {
        /// <summary>动作唯一标识符，例如 "MoveUp" / "EndFlow"</summary>
        public string actionID;

        /// <summary>显示名称，用于 UI</summary>
        public string displayName;

        /// <summary>键盘主键</summary>
        public KeyCode primaryKey = KeyCode.None;

        /// <summary>键盘备用键（None 表示无）</summary>
        public KeyCode alternateKey = KeyCode.None;

        /// <summary>手柄控件（自定义枚举，覆盖完整 Gamepad 控件集）</summary>
        public GamepadCode gamepadKey = GamepadCode.None;

        /// <summary>修饰键（全部按住才触发，仅用于键盘；None 会被忽略）</summary>
        public KeyCode[] modifierKeys = Array.Empty<KeyCode>();

        /// <summary>功能描述</summary>
        public string description;

        public KeyBinding() { }

        public KeyBinding(
            string actionID,
            KeyCode primaryKey = KeyCode.None,
            GamepadCode gamepadKey = GamepadCode.None,
            string displayName = "",
            KeyCode alternateKey = KeyCode.None,
            KeyCode[] modifierKeys = null,
            string description = "")
        {
            this.actionID = actionID;
            this.primaryKey = primaryKey;
            this.gamepadKey = gamepadKey;
            this.displayName = displayName;
            this.alternateKey = alternateKey;
            this.modifierKeys = modifierKeys ?? Array.Empty<KeyCode>();
            this.description = description;
        }

        /// <summary>是否使用某键盘键（主键或备用键）。用于冲突检测。</summary>
        public bool UsesKey(KeyCode key)
            => key != KeyCode.None && (primaryKey == key || alternateKey == key);

        /// <summary>是否使用某手柄控件。用于冲突检测。</summary>
        public bool UsesGamepadKey(GamepadCode key)
            => key != GamepadCode.None && gamepadKey == key;

        /// <summary>深拷贝。</summary>
        public KeyBinding Clone() => new KeyBinding
        {
            actionID = actionID,
            displayName = displayName,
            primaryKey = primaryKey,
            alternateKey = alternateKey,
            gamepadKey = gamepadKey,
            modifierKeys = modifierKeys != null ? (KeyCode[])modifierKeys.Clone() : Array.Empty<KeyCode>(),
            description = description
        };
    }
}