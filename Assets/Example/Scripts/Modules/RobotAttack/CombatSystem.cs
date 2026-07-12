using UnityEngine;

namespace FModa.RA
{
    /// <summary>
    /// 战斗系统 —— 纯逻辑层，负责攻击判定和伤害计算。
    ///
    /// 职责：
    ///   - 以攻击者为中心查找攻击范围内的敌人
    ///   - 计算伤害值并通过 CombatDataCenter.SetHp 写入
    ///
    /// 设计原则：
    ///   - 不依赖 MonoBehaviour，方便后续单元测试
    ///   - 不处理输入、不生成单位、不操作 UI
    ///   - 只通过 CombatDataCenter（而非泛型 GenericTable）访问数据，
    ///     因为 CombatDataCenter 有 typed 的方法和事件，零类型过滤开销
    /// </summary>
    public class CombatSystem
    {
        private readonly CombatDataCenter _dc;
        private readonly float _attackRange;

        public CombatSystem(CombatDataCenter dc, float attackRange = 10f)
        {
            _dc = dc;
            _attackRange = attackRange;
        }

        /// <summary>
        /// 攻击者对其周围 <see cref="_attackRange"/> 内的所有敌对单位发动攻击。
        /// </summary>
        public void ExecuteAttack(GameObject attacker)
        {
            if (attacker == null) return;

            var attackerData = _dc.GetByRobot(attacker);
            if (attackerData == null)
            {
                Debug.LogWarning("[CombatSystem] 攻击者不在数据中心中");
                return;
            }
            if (!attackerData.IsAlive)
            {
                Debug.Log($"[CombatSystem] {attackerData.name} 已死亡");
                return;
            }

            var hits = Physics2D.OverlapCircleAll(attacker.transform.position, _attackRange);
            bool hitAny = false;

            foreach (var hit in hits)
            {
                if (hit.gameObject == attacker) continue;
                if (hit.GetComponent<Robot>() == null) continue;

                var targetData = _dc.GetByRobot(hit.gameObject);
                if (targetData == null || !targetData.IsAlive) continue;

                hitAny = true;

                // ── 伤害计算（业务逻辑，放在 CombatSystem 而非 DataCenter）──
                int oldHp = targetData.hp;
                int newHp = Mathf.Max(0, targetData.hp - attackerData.attack);

                int targetId = _dc.GetIdByRobot(hit.gameObject);
                _dc.SetHp(targetId, newHp);

                Debug.Log($"[CombatSystem] {targetData.name} 受到 {attackerData.attack} 伤害（{oldHp} → {newHp}）");

                if (newHp <= 0)
                {
                    Debug.Log($"[CombatSystem] {targetData.name} 已被 {attackerData.name} 击杀！");
                    _dc.RemoveEntity(targetId);
                }
            }

            if (!hitAny)
            {
                Debug.Log($"[CombatSystem] {attackerData.name} 周围没有可攻击的目标");
            }
        }
    }
}
