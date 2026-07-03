using UnityEngine;

/// <summary>
/// 实体示例
///
/// 直接持有组件的 Data 实例（Inspector 可配置），在 Awake 中创建组件并注册。
/// 修改组件字段只需改组件内的 Data 类，实体无需同步修改。
/// </summary>
public class ExampleEntity : GameEntity
{
    [Header("组件数据")]
    [SerializeField] private CHealth.Data _healthData;
    [SerializeField] private CAttack.Data _attackData;

    protected override void Awake()
    {
        base.Awake();

        // 组件通过 AddComponent&lt;T&gt;() + Init() 模式注册。
        // Init 在 Start 之前完成，组件可在 Start 中安全使用数据。
        var health = gameObject.AddComponent<CHealth>();
        health.Init(this, _healthData);

        var attack = gameObject.AddComponent<CAttack>();
        attack.Init(this, _attackData);
    }
}
