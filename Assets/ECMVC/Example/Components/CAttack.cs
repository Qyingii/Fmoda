using UnityEngine;

// 组件：攻击
//
// 嵌套的 Data 类定义了攻击所需字段，实体直接持有 Data 实例。
// 修改字段只改 Data 一处即可。
public class CAttack : ComponentBehaviour, IUpdate
{
    [System.Serializable]
    public class Data
    {
        [Tooltip("攻击伤害")]   public int   Damage         = 10;
        [Tooltip("攻击间隔（秒）")] public int   AttackInterval = 1;
        [Tooltip("攻击范围")]   public float Range          = 5f;
    }

    private Data   _data;
    private float  _timer;

    public CAttack(UnitEntity entity, Data data) : base(entity)
    {
        _data = data;
    }

    public void Update()
    {
        _timer += Time.deltaTime;
        if (_timer < _data.AttackInterval) return;

        _timer = 0;
        GetTarget();
    }

    private void GetTarget()
    {
        var hits = Physics2D.OverlapCircleAll(m_EgameObject.transform.position, _data.Range);

        EntityBehaviour nearest = null;
        float           minDist = float.MaxValue;

        foreach (var hit in hits)
        {
            var entity = hit.GetComponent<UnitEntity>();

            if (entity == null)  continue;
            if (entity == UnitEntity) continue;

            float dist = Vector2.Distance(
                m_EgameObject.transform.position,
                entity.transform.position);

            if (dist < minDist)
            {
                minDist = dist;
                nearest = entity;
            }
        }

        if (nearest != null)
            HitTarget(nearest);
    }

    private void HitTarget(EntityBehaviour entity)
    {
        if (!entity.HasComponentQ<CHealth>()) return;
        entity.GetComponentQ<CHealth>().TakeDamage(_data.Damage);
    }
}
