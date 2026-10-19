using UnityEngine;
using System;

namespace FModa.InputSystem
{
    /// <summary>
    /// 单个按键绑定数据。纯数据类，可在 Inspector 配置、可序列化到 JSON。
    /// </summary>
    [Serializable]
    public class KeyBinding
    {
        public enum KeyBindingType
        {
            Down, // 按下（一次性，走事件）
            Hold, // 长按（每帧状态，走查询）
            Up    // 释放（一次性，走事件）
        }

        [Tooltip("动作唯一标识符，例如 MoveUp / Attack")]
        public string actionID;

        [Tooltip("显示名称，用于 UI")]
        public string displayName;

        [Tooltip("主按键")]
        public KeyCode primaryKey = KeyCode.None;

        [Tooltip("备用按键（None 表示无）")]
        public KeyCode alternateKey = KeyCode.None;

        [Tooltip("修饰键（全部按住才触发，None 会被忽略）")]
        public KeyCode[] modifierKeys = Array.Empty<KeyCode>();

        [Tooltip("按键使用类型")]
        public KeyBindingType useType = KeyBindingType.Hold;

        [TextArea]
        [Tooltip("功能描述")]
        public string description;

        public KeyBinding() { }

        public KeyBinding(
            string actionID,
            KeyCode primaryKey,
            KeyBindingType useType,
            string displayName = "",
            KeyCode alternateKey = KeyCode.None,
            KeyCode[] modifierKeys = null,
            string description = "")
        {
            this.primaryKey = primaryKey;
            this.useType = useType;
            this.actionID = actionID;
            this.displayName = displayName;
            this.alternateKey = alternateKey;
            this.modifierKeys = modifierKeys ?? Array.Empty<KeyCode>();
            this.description = description;
        }

        /// <summary>
        /// 检查本帧是否触发。
        /// - Down/Up：本帧一次性触发
        /// - Hold：本帧是否按住
        /// </summary>
        public bool IsTriggered()
        {
            // 修饰键前置短路：无修饰键直接跳过循环
            if (modifierKeys != null && modifierKeys.Length > 0)
            {
                for (int i = 0; i < modifierKeys.Length; i++)
                {
                    KeyCode m = modifierKeys[i];
                    if (m != KeyCode.None && !Input.GetKey(m))
                        return false;
                }
            }

            switch (useType)
            {
                case KeyBindingType.Down:
                    return Input.GetKeyDown(primaryKey) ||
                           (alternateKey != KeyCode.None && Input.GetKeyDown(alternateKey));

                case KeyBindingType.Hold:
                    return Input.GetKey(primaryKey) ||
                           (alternateKey != KeyCode.None && Input.GetKey(alternateKey));

                case KeyBindingType.Up:
                    return Input.GetKeyUp(primaryKey) ||
                           (alternateKey != KeyCode.None && Input.GetKeyUp(alternateKey));

                default:
                    return false;
            }
        }

        /// <summary>
        /// 是否使用某个按键（主键或备用键）。用于冲突检测。
        /// </summary>
        public bool UsesKey(KeyCode key)
        {
            return key != KeyCode.None && (primaryKey == key || alternateKey == key);
        }

        public KeyBinding Clone()
        {
            return new KeyBinding
            {
                actionID = this.actionID,
                displayName = this.displayName,
                primaryKey = this.primaryKey,
                alternateKey = this.alternateKey,
                modifierKeys = this.modifierKeys != null
                    ? (KeyCode[])this.modifierKeys.Clone()
                    : Array.Empty<KeyCode>(),
                useType = this.useType,
                description = this.description
            };
        }
    }
}