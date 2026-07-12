using System;
using UnityEngine;

namespace FModa.RA
{
    /// <summary>
    /// Robot Attack 模块 —— 战斗子系统的编排者。
    ///
    /// 职责（只做协调，不做具体实现）：
    ///   1. 生成单位（Instantiate 预制体）
    ///   2. 将单位数据写入 DataManager.Combat
    ///   3. 订阅 Robot.onAttack 事件，委托给 CombatSystem
    ///
    /// 具体实现（攻击判定、伤害计算）全部在 <see cref="CombatSystem"/> 中。
    /// 数据存储和变更事件在 <see cref="CombatDataCenter"/> 中。
    /// 输入捕获在 <see cref="Robot"/> 的每帧 Update 中。
    /// </summary>
    public class RAManager : MonoBehaviour, IRA, IModule
    {
        public Type InterfaceType => typeof(IRA);
        public int Priority => 5;
        public Action OnFlowEnd{get;set;} //流程结束时触发，ExampleFlow 订阅此事件后切换流程

        [Header("机器人预制体（在 Inspector 中拖拽赋值）")]
        public GameObject robotPrefab1;
        public GameObject robotPrefab2;

        private CombatSystem _combat;
        private CombatDataCenter _dc => DataManager.Combat;//数据中心，用于存储战斗数据
        private GameObject _robot1Instance;
        private GameObject _robot2Instance;

        public void OnStart()
        {
            _combat = new CombatSystem(DataManager.Combat);

            // 1. 派生单位
            _robot1Instance = Instantiate(robotPrefab1, new Vector3(-1, 1, 0), Quaternion.identity);
            _robot2Instance = Instantiate(robotPrefab2, new Vector3(0, 0, 0), Quaternion.identity);

            // 2. 写入 CombatDataCenter
            DataManager.Combat.AddEntity(new RAData
            {
                robot = _robot1Instance,
                name = "机器人1",
                maxHp = 100,
                hp = 100,
                attack = 10
            });
            DataManager.Combat.AddEntity(new RAData
            {
                robot = _robot2Instance,
                name = "机器人2",
                maxHp = 100,
                hp = 100,
                attack = 5
            });

            // 3. 订阅 Robot 的攻击信号
            _robot1Instance.GetComponent<Robot>().onAttack += OnRobotAttack;
            _robot2Instance.GetComponent<Robot>().onAttack += OnRobotAttack;
            _dc.OnEntityRemoved += (id) =>                      //有单位死亡时流程结束
            {
                OnFlowEnd?.Invoke();
            };

            Debug.Log("[RAManager] 初始化完成。按 A 键 → 机器人1 攻击 | 按 S 键 → 机器人2 攻击");
        }

        public void Attack(GameObject attacker)
        {
            _combat.ExecuteAttack(attacker);
        }

        private void OnRobotAttack(GameObject robot)
        {
            Attack(robot);
        }

    }
}
