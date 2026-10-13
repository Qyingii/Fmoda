using System;
using UnityEngine;

namespace FModa.RA
{
        ///  <summary>
        ///  Robot Attack 模块 —— 战斗子系统的编排者。
        ///  职责（只做协调，不做具体实现）：
        ///    1. 生成单位（Instantiate 预制体）
        ///    2. 将单位数据写入 DataManager.Combat
        ///    3. 订阅 Robot.onAttack 事件，委托给 CombatSystem
        ///  具体实现（攻击判定、伤害计算）全部在 <see cref="CombatSystem"/> 中。
        ///  数据存储和变更事件在 <see cref="CombatDataCenter"/> 中。
        ///  输入捕获在 <see cref="Robot"/> 的每帧 Update 中。
        ///  </summary>
    public interface IRAManager
    {
        ///  <summary>
        ///  A
        ///  </summary>
        int A { get; set; }

        /// <summary>
        /// 流程结束时触发，ExampleFlow 订阅此事件后切换流程
        /// </summary>
        event Action OnFlowEnd2;

        /// <summary>
        /// B
        /// </summary>
        int B { get; set; }

        Type InterfaceType { get; }

        int Priority { get; }

        /// <summary>
        /// 流程结束时触发，ExampleFlow 订阅此事件后切换流程
        /// </summary>
        Action OnFlowEnd { get; set; }

        GameObject robotPrefab1 { get; set; }

        GameObject robotPrefab2 { get; set; }

        void OnStart();

        void Attack(GameObject attacker);

    }
}
