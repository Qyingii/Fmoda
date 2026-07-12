using System;
using UnityEngine;

namespace FModa.RA
{
    /// <summary>
    /// Robot Attack 模块对外接口。
    /// 外部代码通过 <c>GF.GetModule&lt;IRA&gt;()</c> 获取模块引用并调用其方法，
    /// 无需依赖具体类型 <c>RAManager</c>。
    /// </summary>
    public interface IRA
    {
        /// <summary>
        /// 让指定单位对其攻击范围内的敌人发起一次攻击。
        /// </summary>
        /// <param name="attacker">发起攻击的单位 GameObject</param>
        void Attack(GameObject attacker);

        /// <summary>
        /// 流程结束时触发的事件，ExampleFlow 订阅此事件后切换流程。
        /// </summary>
        Action OnFlowEnd { get; set; } //流程结束时触发，ExampleFlow 订阅此事件后切换流程
    }
}
