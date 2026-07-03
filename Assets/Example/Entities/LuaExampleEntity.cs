using UnityEngine;

/// <summary>
/// Lua/C# 混合实体
///
/// C# 组件直接在 Awake 中通过 AddComponent 创建（与 ExampleEntity 相同），
/// Lua 组件通过 Inspector 的 _luaComponents 数组按名称注入。
///
/// 修改 C# 组件字段只需改组件内的 Data 类，实体无需同步修改。
/// </summary>
public class LuaExampleEntity : GameEntity
{
    [Header("Lua 组件")]
    [Tooltip("按顺序填写 Lua 组件名称（通过 Game.AddComponent 注册的名称）")]
    [SerializeField] private string[] _luaComponents;

    [Header("C# 组件数据")]
    [SerializeField] private CHealth.Data _healthData = new();
    [SerializeField] private CAttack.Data _attackData = new();

    protected override void Awake()
    {
        base.Awake();

        // C# 组件
        var health = gameObject.AddComponent<CHealth>();
        health.Init(this, _healthData);

        var attack = gameObject.AddComponent<CAttack>();
        attack.Init(this, _attackData);

        // Lua 桥接组件（MonoBehaviour，与 C# 组件使用相同的 Unity 生命周期）
        gameObject.AddComponent<CustomComponent>();
    }

    private void Start()
    {
        ApplyLuaComponents();
    }

    private void ApplyLuaComponents()
    {
        if (_luaComponents == null) return;

        if (ModManager.Instance == null)
        {
            Debug.LogWarning($"[{name}] ModManager 不可用，跳过 Lua 组件应用。");
            return;
        }

        var customComponent = GetComponent<CustomComponent>();
        if (customComponent == null)
        {
            Debug.LogWarning($"[{name}] CustomComponent 未找到，无法挂载 Lua 组件。");
            return;
        }

        foreach (var compName in _luaComponents)
        {
            if (!string.IsNullOrEmpty(compName))
                ModManager.Instance.API.ApplyComponent(compName, this, customComponent);
        }
    }
}
