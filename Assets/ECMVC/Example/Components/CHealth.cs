using UnityEngine;

// 组件：生命值
//
// 嵌套的 Data 类定义了组件所需的所有字段，实体直接持有 Data 实例即可。
// 修改字段只需修改 Data 一处，无需接口声明、实体字段、实体属性三处同步。
public class CHealth : ComponentBehaviour, IStart, IOnDestroy
{
    [System.Serializable]
    public class Data
    {
        [Tooltip("最大生命值")] public int MaxHp = 100;
        [Tooltip("当前生命值")] public int CurrentHp;
    }

    private Data _data;

    public CHealth(UnitEntity entity, Data data) : base(entity)
    {
        _data = data;
    }

    public void Start()
    {
        _data.CurrentHp = _data.MaxHp;
        Debug.Log($"[{m_UnitEntity.name}] 初始化血量 {_data.CurrentHp}/{_data.MaxHp}");
    }

    public void OnDestroy()
    {
        Debug.Log($"[{m_UnitEntity.name}] 已销毁");
    }

    public void TakeDamage(int damage)
    {
        _data.CurrentHp = Mathf.Max(0, _data.CurrentHp - damage);
        Debug.Log($"[{m_UnitEntity.name}] 受到 {damage} 伤害，剩余 {_data.CurrentHp}/{_data.MaxHp}");

        if (_data.CurrentHp <= 0)
            Object.Destroy(m_EgameObject);
    }

    public void Heal(int amount)
    {
        _data.CurrentHp = Mathf.Min(_data.MaxHp, _data.CurrentHp + amount);
        Debug.Log($"[{m_UnitEntity.name}] 恢复 {amount} 血量，当前 {_data.CurrentHp}/{_data.MaxHp}");
    }
}
