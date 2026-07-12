using UnityEngine;

namespace ECMVC.RA
{
    /// <summary>
    /// 战斗系统 —— 纯逻辑层，负责攻击判定和伤害计算。
    ///
    /// 职责：
    ///   - 以攻击者为中心查找攻击范围内的敌人
    ///   - 计算伤害值并通过 DataCenter.SetEntityHp 写入
    ///
    /// 设计原则：
    ///   - 不依赖 MonoBehaviour，方便后续单元测试
    ///   - 不直接持有任何 GameObject 引用（通过参数传入）
    ///   - 不处理输入、不生成单位、不操作 UI
    /// </summary>
    public class CombatSystem
    {
        private readonly DataCenter _dataCenter;
        private readonly float _attackRange;

        /// <param name="dataCenter">数据中心（通常传入 GF.dataCenter）</param>
        /// <param name="attackRange">攻击半径，单位：Unity 世界单位</param>
        public CombatSystem(DataCenter dataCenter, float attackRange = 10f)
        {
            _dataCenter = dataCenter;
            _attackRange = attackRange;
        }

        /// <summary>
        /// 攻击者对其周围 <see cref="_attackRange"/> 内的所有敌对单位发动攻击。
        /// 每个目标独立计算伤害，通过 DataCenter.SetEntityHp 写入血量并触发事件。
        /// </summary>
        /// <param name="attacker">发起攻击的 GameObject</param>
        public void ExecuteAttack(GameObject attacker)
        {
            if (attacker == null) return;

            var attackerData = _dataCenter.GetEntityByRobot(attacker);
            if (attackerData == null)
            {
                Debug.LogWarning("[CombatSystem] 攻击者不在数据中心中，攻击被忽略");
                return;
            }
            if (!attackerData.IsAlive)
            {
                Debug.Log($"[CombatSystem] {attackerData.name} 已死亡，无法攻击");
                return;
            }

            // 查找攻击范围内的碰撞体
            var hits = Physics2D.OverlapCircleAll(attacker.transform.position, _attackRange);
            bool hitAny = false;

            foreach (var hit in hits)
            {
                // 跳过自身
                if (hit.gameObject == attacker) continue;

                // 只攻击带有 Robot 组件的对象（相当于"可攻击标记"）
                if (hit.GetComponent<Robot>() == null) continue;

                var targetData = _dataCenter.GetEntityByRobot(hit.gameObject);
                if (targetData == null || !targetData.IsAlive) continue;

                hitAny = true;

                // ── 伤害计算（业务逻辑，放在 CombatSystem 而非 DataCenter）──
                int oldHp = targetData.hp;
                int newHp = Mathf.Max(0, targetData.hp - attackerData.attack);
                _dataCenter.SetEntityHp(targetData.id, newHp);
                Debug.Log($"[CombatSystem] {targetData.name} 受到 {attackerData.attack} 伤害（{oldHp} → {newHp}）");

                if (newHp <= 0)
                {
                    Debug.Log($"[CombatSystem] {targetData.name} 已被 {attackerData.name} 击杀！");
                }
            }

            if (!hitAny)
            {
                Debug.Log($"[CombatSystem] {attackerData.name} 周围 10 码内没有可攻击的目标");
            }
        }
    }
}
