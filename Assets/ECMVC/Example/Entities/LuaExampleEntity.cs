using UnityEngine;

// Lua/C# 混合实体
//
// C# 组件直接在 Awake 中创建注册（与 ExampleEntity 相同），
// Lua 组件通过 Inspector 的 _luaComponents 数组按名称注入。
//
// 修改 C# 组件字段只需改组件内的 Data 类，实体无需同步修改。
public class LuaExampleEntity : UnitEntity
{
    [Header("Lua 组件")]
    [Tooltip("按顺序填写 Lua 组件名称（通过 Game.AddComponent 注册的名称）")]
    [SerializeField] private string[] _luaComponents;

    [Header("C# 组件数据")]
    [SerializeField] private CHealth.Data _healthData = new();
    [SerializeField] private CAttack.Data _attackData = new();

    private CustomComponent _customComponent;
    private CHealth _cHealth;
    private CAttack _cAttack;

    protected override void Awake()
    {
        // C# 组件直接创建注册
        _cHealth = new CHealth(this, _healthData);
        _cAttack = new CAttack(this, _attackData);
        AddComponentQ(_cHealth);
        AddComponentQ(_cAttack);

        // Lua 桥接组件
        _customComponent = new CustomComponent(this);
        AddComponentQ(_customComponent);

        base.Awake();
    }

    private void Start()
    {
        ApplyLuaComponents();
    }

    private void ApplyLuaComponents()
    {
        if (_luaComponents == null) return;

        if (LuaManager.Instance == null)
        {
            Debug.LogWarning($"[{name}] LuaManager 不可用，跳过 Lua 组件应用。");
            return;
        }

        foreach (var compName in _luaComponents)
        {
            if (!string.IsNullOrEmpty(compName))
                LuaManager.Instance.API.ApplyComponent(compName, this, _customComponent);
        }
    }
}
