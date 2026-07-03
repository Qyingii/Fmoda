using UnityEngine;

/// <summary>
/// C# 组件工厂注册（运行时动态添加场景）
///
/// 实体在 Awake 中直接创建 C# 组件（推荐方式，可使用 Inspector 配置 Data），
/// 此脚本仅作为在运行时通过名称动态添加组件的备选方案。
///
/// 用法：在场景中任意 GameObject 上挂载此脚本即可。
/// 随后可在 Lua 或代码中通过 ModManager.Instance.API.ApplyComponent("CHealth", entity, null) 调用。
/// </summary>
[DefaultExecutionOrder(-100)]
public class ComponentRegistry : MonoBehaviour
{
    private void Start()
    {
        var api = ModManager.Instance?.API;
        if (api == null)
        {
            Debug.LogError("[ComponentRegistry] ModManager 不可用。");
            return;
        }

        api.RegisterCSharpComponent("CHealth", entity =>
        {
            if (entity.GetComponent<CHealth>() == null)
                entity.gameObject.AddComponent<CHealth>().Init(entity, new CHealth.Data());
        });

        api.RegisterCSharpComponent("CAttack", entity =>
        {
            if (entity.GetComponent<CAttack>() == null)
                entity.gameObject.AddComponent<CAttack>().Init(entity, new CAttack.Data());
        });

        Debug.Log("[ComponentRegistry] C# 组件工厂注册完成（CHealth, CAttack，使用默认 Data）");
    }
}
