using UnityEngine;

namespace FModa.InputSystem
{
    /// <summary>
    /// KeyCode（键盘）→ Input System binding path 映射。
    /// 手柄请使用 GamepadCode + GamepadControlPath（在 InputData.cs 中）。
    /// </summary>
    public static class KeyCodeToPath
    {
        /// <summary>
        /// 把键盘 KeyCode 翻译成 Input System binding path。
        /// 返回 null 表示无法映射（None 或非键盘键）。
        /// </summary>
        public static string ToPath(KeyCode key)
        {
            if (key == KeyCode.None) return null;

            // 字母 A-Z
            if (key >= KeyCode.A && key <= KeyCode.Z)
                return $"<Keyboard>/{(char)('a' + (key - KeyCode.A))}";

            // 数字 0-9（主键盘区）
            if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9)
                return $"<Keyboard>/{key - KeyCode.Alpha0}";

            // 小键盘
            if (key >= KeyCode.Keypad0 && key <= KeyCode.Keypad9)
                return $"<Keyboard>/numpad{key - KeyCode.Keypad0}";

            // 功能键 F1-F12
            if (key >= KeyCode.F1 && key <= KeyCode.F12)
                return $"<Keyboard>/f{key - KeyCode.F1 + 1}";

            // 常用特殊键
            switch (key)
            {
                case KeyCode.Space:        return "<Keyboard>/space";
                case KeyCode.Return:       return "<Keyboard>/enter";
                case KeyCode.Escape:       return "<Keyboard>/escape";
                case KeyCode.Tab:          return "<Keyboard>/tab";
                case KeyCode.Backspace:    return "<Keyboard>/backspace";
                case KeyCode.LeftShift:    return "<Keyboard>/leftShift";
                case KeyCode.RightShift:   return "<Keyboard>/rightShift";
                case KeyCode.LeftControl:  return "<Keyboard>/leftCtrl";
                case KeyCode.RightControl: return "<Keyboard>/rightCtrl";
                case KeyCode.LeftAlt:      return "<Keyboard>/leftAlt";
                case KeyCode.RightAlt:     return "<Keyboard>/rightAlt";
                case KeyCode.UpArrow:      return "<Keyboard>/upArrow";
                case KeyCode.DownArrow:    return "<Keyboard>/downArrow";
                case KeyCode.LeftArrow:    return "<Keyboard>/leftArrow";
                case KeyCode.RightArrow:   return "<Keyboard>/rightArrow";
                case KeyCode.Comma:        return "<Keyboard>/comma";
                case KeyCode.Period:       return "<Keyboard>/period";
                case KeyCode.Slash:        return "<Keyboard>/slash";
                case KeyCode.Semicolon:    return "<Keyboard>/semicolon";
                case KeyCode.Quote:        return "<Keyboard>/quote";
                case KeyCode.LeftBracket:  return "<Keyboard>/leftBracket";
                case KeyCode.RightBracket: return "<Keyboard>/rightBracket";
                case KeyCode.Backslash:    return "<Keyboard>/backslash";
                case KeyCode.Minus:        return "<Keyboard>/minus";
                case KeyCode.Equals:       return "<Keyboard>/equals";
                case KeyCode.BackQuote:    return "<Keyboard>/backquote";
            }

            return null;
        }
    }
}