using System;
using UnityEngine;

/// <summary>
/// 机器人可视化层 —— 标记可攻击的 GameObject，捕获输入，触发攻击信号。
///
/// 职责说明：
///   - 只负责在 Unity 每帧 Update 中检测自己的按键（GetKeyDown 必须在原生 Update 中才可靠）
///   - 检测到按键后通过 onAttack 事件通知模块层（RAManager）
///   - 不包含任何攻击逻辑、伤害计算、数据修改
///
/// 为什么输入检测放在这里而非模块的 IModuleUpdate：
///   GF 的 IModuleUpdate 按逻辑帧触发（默认 0.1s 一次），而 Input.GetKeyDown()
///   只在按键那帧返回 true。放在模块更新中会丢失大量输入。
///   所以"输入捕获"放在 MonoBehaviour.Update（每帧），"输入处理"放在模块层。
/// </summary>
public class Robot : MonoBehaviour
{
    [Header("攻击按键（在预制体中分别设置）")]
    public KeyCode attackKey = KeyCode.A;

    /// <summary>
    /// 攻击信号 —— 按键时触发。
    /// RAManager 订阅此事件后委托给 CombatSystem 执行实际攻击。
    /// </summary>
    public Action<GameObject> onAttack;

    private void Update()
    {
        if (Input.GetKeyDown(attackKey))
        {
            onAttack?.Invoke(gameObject);
        }
    }
}
