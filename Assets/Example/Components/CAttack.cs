using UnityEngine;

/// <summary>
/// 组件：攻击
///
/// 通过 Physics2D.OverlapCircleAll 查找范围内最近的敌人，
/// 对其 CHealth 组件造成伤害。
///
/// 使用方式：
///   var attack = gameObject.AddComponent&lt;CAttack&gt;();
///   attack.Init(entity, data);
/// </summary>
public class CAttack : MonoBehaviour
{
    [System.Serializable]
    public class Data
    {
        [Tooltip("攻击伤害")]     public int   Damage         = 10;
        [Tooltip("攻击间隔（秒）")] public float AttackInterval = 1;
        [Tooltip("攻击范围")]     public float Range          = 5f;
    }

    private Data  _data;
    private float _timer;
    private GameEntity _entity;

    public void Init(GameEntity entity, Data data)
    {
        _entity = entity;
        _data = data;
    }

    private void Update()
    {
        _timer += Time.deltaTime;
        if (_timer < _data.AttackInterval) return;

        _timer = 0;
        FindAndHitTarget();
    }

    private void FindAndHitTarget()
    {
        var hits = Physics2D.OverlapCircleAll(transform.position, _data.Range);

        GameEntity nearest = null;
        float       minDist = float.MaxValue;

        foreach (var hit in hits)
        {
            var entity = hit.GetComponent<GameEntity>();
            if (entity == null)  continue;
            if (entity == _entity) continue;

            float dist = Vector2.Distance(transform.position, entity.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = entity;
            }
        }

        if (nearest != null)
            nearest.GetComponent<CHealth>()?.TakeDamage(_data.Damage);
    }
}
