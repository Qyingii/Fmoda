using System;
using System.Collections.Generic;
using UnityEngine;

namespace FModa.InputSystem
{
    public static class InputInit
    {
        /// <summary>
        /// 初始化输入系统,将你需要绑定的按键添加到列表中
        /// </summary>
        /// <returns></returns>
        public static List<KeyBinding> InitInputSystem()
        {
            var list = new List<KeyBinding>
            {
                // 单位控制
                new KeyBinding("UnitDeductionOfBlood", KeyCode.Q),
                new KeyBinding("MoveUp",               KeyCode.W),
                new KeyBinding("OpenUI",               KeyCode.E),
                new KeyBinding("Spawn",                KeyCode.R),

                // 结束流程
                new KeyBinding("EndFlow",              KeyCode.Space),
            };
            return list;
        }

    }
}
