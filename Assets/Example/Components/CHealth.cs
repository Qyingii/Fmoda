using UnityEngine;

/// <summary>
/// 组件：生命值
///
/// 嵌套的 Data 类定义了组件所需的所有字段，实体直接持有 Data 实例。
/// 修改字段只需改 Data 一处，无需接口声明、实体字段、实体属性三处同步。
///
/// 使用方式：
///   var health = gameObject.AddComponent&lt;CHealth&gt;();
///   health.Init(entity, data);
/// </summary>
public class CHealth : MonoBehaviour
{
    [System.Serializable]
    public class Data
    {
        [Tooltip("最大生命值")] public int MaxHp = 100;
        [Tooltip("当前生命值")] public int CurrentHp;
    }

    private Data _data;
    private GameEntity _entity;

    /// <summary>
    /// 在 AddComponent 后立即调用，将数据传入组件。
    /// Init 在 Start 之前执行完毕，Start 中可安全使用数据。
    /// </summary>
    public void Init(GameEntity entity, Data data)
    {
        _entity = entity;
        _data = data;
    }

    private void Start()
    {
        _data.CurrentHp = _data.MaxHp;
        Debug.Log($"[{name}] 初始化血量 {_data.CurrentHp}/{_data.MaxHp}");
    }

    private void OnDestroy()
    {
        Debug.Log($"[{name}] 已销毁");
    }

    public void TakeDamage(int damage)
    {
        _data.CurrentHp = Mathf.Max(0, _data.CurrentHp - damage);
        Debug.Log($"[{name}] 受到 {damage} 伤害，剩余 {_data.CurrentHp}/{_data.MaxHp}");

        if (_data.CurrentHp <= 0)
            Destroy(gameObject);
    }

    public void Heal(int amount)
    {
        _data.CurrentHp = Mathf.Min(_data.MaxHp, _data.CurrentHp + amount);
        Debug.Log($"[{name}] 恢复 {amount} 血量，当前 {_data.CurrentHp}/{_data.MaxHp}");
    }
}
