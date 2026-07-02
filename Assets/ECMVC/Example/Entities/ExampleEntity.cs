using UnityEngine;

// 实体示例
//
// 直接持有组件的 Data 实例（Inspector 可配置），在 Awake 中创建组件并注册。
// 修改组件字段只需改组件内的 Data 类，实体无需同步修改。
public class ExampleEntity : UnitEntity
{
    [Header("组件数据")]
    [SerializeField] private CHealth.Data _healthData = new();
    [SerializeField] private CAttack.Data _attackData = new();

    private CHealth _cHealth;
    private CAttack _cAttack;

    protected override void Awake()
    {
        _cHealth = new CHealth(this, _healthData);
        _cAttack = new CAttack(this, _attackData);

        AddComponentQ(_cHealth);
        AddComponentQ(_cAttack);

        base.Awake();
    }
}
