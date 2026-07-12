using System;
using UnityEngine;

namespace ECMVC.RA
{
    /// <summary>
    /// Robot Attack 模块 —— 战斗子系统的编排者。
    ///
    /// 职责（只做协调，不做具体实现）：
    ///   1. 生成单位（Instantiate 预制体）
    ///   2. 将单位数据注册到 DataCenter
    ///   3. 订阅 Robot.onAttack 事件，将攻击请求委托给 CombatSystem
    ///
    /// 具体实现（攻击判定、伤害计算）全部在 <see cref="CombatSystem"/> 中。
    /// 数据存储和事件通知全部在 <see cref="DataCenter"/> 中。
    /// 输入捕获在 <see cref="Robot"/> 的每帧 Update 中（Input.GetKeyDown 必须每帧检测）。
    /// </summary>
    public class RAManager : MonoBehaviour, IRA, IModule, IModuleStart
    {
        public Type InterfaceType => typeof(IRA);
        public int Priority => 5;

        [Header("机器人预制体（在 Inspector 中拖拽赋值）")]
        public GameObject robotPrefab1;
        public GameObject robotPrefab2;

        // ── 私有成员 ──

        private CombatSystem _combat;
        private GameObject _robot1Instance;
        private GameObject _robot2Instance;

        // ── IModuleStart —— 由 GF 模块系统调用 ──

        public void OnStart()
        {
            _combat = new CombatSystem(GF.dataCenter);

            // 1. 派生单位（生成场景实例）
            _robot1Instance = Instantiate(robotPrefab1, new Vector3(-1, 1, 0), Quaternion.identity);
            _robot2Instance = Instantiate(robotPrefab2, new Vector3(0, 0, 0), Quaternion.identity);

            // 2. 将数据写入 DataCenter
            GF.dataCenter.AddEntity(new RAData
            {
                robot = _robot1Instance,
                name = "机器人1",
                maxHp = 100,
                hp = 100,
                attack = 10
            });
            GF.dataCenter.AddEntity(new RAData
            {
                robot = _robot2Instance,
                name = "机器人2",
                maxHp = 100,
                hp = 100,
                attack = 5
            });

            // 3. 订阅 Robot 的攻击信号（Robot.Update 每帧检测按键，触发此事件）
            _robot1Instance.GetComponent<Robot>().onAttack += OnRobotAttack;
            _robot2Instance.GetComponent<Robot>().onAttack += OnRobotAttack;

            Debug.Log("[RAManager] 初始化完成。按 A 键 → 机器人1 攻击 | 按 S 键 → 机器人2 攻击");
        }

        // ── IRA 接口实现 ──

        /// <summary> 让指定单位发起一次范围攻击（委托给 CombatSystem）</summary>
        public void Attack(GameObject attacker)
        {
            _combat.ExecuteAttack(attacker);
        }

        // ── Robot 事件回调 ──

        private void OnRobotAttack(GameObject robot)
        {
            Attack(robot);
        }
    }
}
