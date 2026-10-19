namespace FModa.InputSystem
{
    /// <summary>
    /// 手柄控件枚举：覆盖 Input System 的 Gamepad 抽象控件集。
    /// 与 KeyCode 解耦，不依赖平台编号，跨设备（Xbox / PS / Switch）自动适配。
    ///
    /// 编号规则：
    /// - None = 0
    /// - 面键：ButtonSouth .. ButtonNorth = 1..4
    /// - 肩键/菜单键：LeftShoulder .. Start = 5..8
    /// - 十字键：DpadUp .. DpadRight = 20..23
    /// - 左摇杆方向：LeftStickUp .. LeftStickRight = 30..33
    /// - 右摇杆方向：RightStickUp .. RightStickRight = 40..43
    /// - 扳机：LeftTrigger / RightTrigger = 50 / 51
    /// - 摇杆按压：LeftStickPress / RightStickPress = 60 / 61
    /// </summary>
    public enum GamepadCode
    {
        None = 0,

        // ---- 面键（Xbox 命名，PS/Switch 由 Input System 自动适配）----
        ButtonSouth = 1,   // A / Cross
        ButtonEast  = 2,   // B / Circle
        ButtonWest  = 3,   // X / Square
        ButtonNorth = 4,   // Y / Triangle

        // ---- 肩键 / 菜单键 ----
        LeftShoulder  = 5,
        RightShoulder = 6,
        Select        = 7,   // Back / Share
        Start         = 8,   // Start / Options

        // ---- 十字键 ----
        DpadUp    = 20,
        DpadDown  = 21,
        DpadLeft  = 22,
        DpadRight = 23,

        // ---- 左摇杆方向（Button 化包装）----
        LeftStickUp    = 30,
        LeftStickDown  = 31,
        LeftStickLeft  = 32,
        LeftStickRight = 33,

        // ---- 右摇杆方向 ----
        RightStickUp    = 40,
        RightStickDown  = 41,
        RightStickLeft  = 42,
        RightStickRight = 43,

        // ---- 扳机 ----
        LeftTrigger  = 50,
        RightTrigger = 51,

        // ---- 摇杆按压 ----
        LeftStickPress  = 60,
        RightStickPress = 61,
    }

    /// <summary>
    /// GamepadCode → Input System binding path / 显示名。
    /// 所有路径均来自 Gamepad 抽象控件，跨平台有效。
    /// </summary>
    public static class GamepadControlPath
    {
        /// <summary>
        /// 把 GamepadCode 翻译成 Input System binding path。
        /// 返回 null 表示 None。
        /// </summary>
        public static string ToPath(GamepadCode c)
        {
            switch (c)
            {
                case GamepadCode.None: return null;

                case GamepadCode.ButtonSouth: return "<Gamepad>/buttonSouth";
                case GamepadCode.ButtonEast:  return "<Gamepad>/buttonEast";
                case GamepadCode.ButtonWest:  return "<Gamepad>/buttonWest";
                case GamepadCode.ButtonNorth: return "<Gamepad>/buttonNorth";

                case GamepadCode.LeftShoulder:  return "<Gamepad>/leftShoulder";
                case GamepadCode.RightShoulder: return "<Gamepad>/rightShoulder";
                case GamepadCode.Select:        return "<Gamepad>/select";
                case GamepadCode.Start:         return "<Gamepad>/start";

                case GamepadCode.DpadUp:    return "<Gamepad>/dpad/up";
                case GamepadCode.DpadDown:  return "<Gamepad>/dpad/down";
                case GamepadCode.DpadLeft:  return "<Gamepad>/dpad/left";
                case GamepadCode.DpadRight: return "<Gamepad>/dpad/right";

                case GamepadCode.LeftStickUp:    return "<Gamepad>/leftStick/up";
                case GamepadCode.LeftStickDown:  return "<Gamepad>/leftStick/down";
                case GamepadCode.LeftStickLeft:  return "<Gamepad>/leftStick/left";
                case GamepadCode.LeftStickRight: return "<Gamepad>/leftStick/right";

                case GamepadCode.RightStickUp:    return "<Gamepad>/rightStick/up";
                case GamepadCode.RightStickDown:  return "<Gamepad>/rightStick/down";
                case GamepadCode.RightStickLeft:  return "<Gamepad>/rightStick/left";
                case GamepadCode.RightStickRight: return "<Gamepad>/rightStick/right";

                case GamepadCode.LeftTrigger:  return "<Gamepad>/leftTrigger";
                case GamepadCode.RightTrigger: return "<Gamepad>/rightTrigger";

                case GamepadCode.LeftStickPress:  return "<Gamepad>/leftStickPress";
                case GamepadCode.RightStickPress: return "<Gamepad>/rightStickPress";
            }
            return null;
        }

        /// <summary>显示名称（用于 UI 提示 / 调试打印）。</summary>
        public static string DisplayName(GamepadCode c)
        {
            switch (c)
            {
                case GamepadCode.None: return "无";

                case GamepadCode.ButtonSouth: return "A";
                case GamepadCode.ButtonEast:  return "B";
                case GamepadCode.ButtonWest:  return "X";
                case GamepadCode.ButtonNorth: return "Y";

                case GamepadCode.LeftShoulder:  return "LB";
                case GamepadCode.RightShoulder: return "RB";
                case GamepadCode.Select:        return "Select";
                case GamepadCode.Start:         return "Start";

                case GamepadCode.DpadUp:    return "D-Pad ↑";
                case GamepadCode.DpadDown:  return "D-Pad ↓";
                case GamepadCode.DpadLeft:  return "D-Pad ←";
                case GamepadCode.DpadRight: return "D-Pad →";

                case GamepadCode.LeftStickUp:    return "左摇杆 ↑";
                case GamepadCode.LeftStickDown:  return "左摇杆 ↓";
                case GamepadCode.LeftStickLeft:  return "左摇杆 ←";
                case GamepadCode.LeftStickRight: return "左摇杆 →";

                case GamepadCode.RightStickUp:    return "右摇杆 ↑";
                case GamepadCode.RightStickDown:  return "右摇杆 ↓";
                case GamepadCode.RightStickLeft:  return "右摇杆 ←";
                case GamepadCode.RightStickRight: return "右摇杆 →";

                case GamepadCode.LeftTrigger:  return "LT";
                case GamepadCode.RightTrigger: return "RT";

                case GamepadCode.LeftStickPress:  return "LS";
                case GamepadCode.RightStickPress: return "RS";
            }
            return c.ToString();
        }
    }
}