using System;
using System.Collections.Generic;
using MoonSharp.Interpreter;
using UnityEngine;

// Lua 中通过全局变量 Game 访问此对象。
//
// 典型 Lua 组件用法：
//   local MyComp = {}
//   MyComp.Start  = function(self) self.timer = 0 end
//   MyComp.Update = function(self) self.timer = self.timer + 1 end
//   Game.AddComponent("MyComp", MyComp)
//
// C# 组件注册（游戏初始化时，在 Lua Mod 加载前调用）：
//   api.RegisterCSharpComponent("CHealth", entity => {
//       entity.AddComponentQDynamic(new CHealth(entity, new CHealth.Data()));
//   });
//
// Inspector 字段（_components）中同时填写 C# 组件名和 Lua 组件名，框架自动路由：
//   先查 C# 工厂表 → 再查 Lua 模板表 → 否则 Warning

[MoonSharpUserData]
public class LuaGameAPI
{
    private readonly Dictionary<string, Table>           _templates       = new Dictionary<string, Table>();
    private readonly Dictionary<string, Action<UnitEntity>> _csharpFactories = new Dictionary<string, Action<UnitEntity>>();
    private readonly Script _script;

    [MoonSharpHidden]
    public LuaGameAPI(Script script) => _script = script;

    // ── Lua 可调用 API ────────────────────────────────────────────────────────

    public void AddComponent(string name, Table template)
    {
        if (_templates.ContainsKey(name))
        {
            Debug.LogWarning($"[Lua] 组件模板 '{name}' 已注册，跳过。");
            return;
        }
        _templates.Add(name, template);
    }

    public void RemoveComponent(string name) => _templates.Remove(name);

    public bool HasComponent(string name) =>
        _csharpFactories.ContainsKey(name) || _templates.ContainsKey(name);

    public void Log(string msg)        => Debug.Log($"[Lua] {msg}");
    public void LogWarning(string msg) => Debug.LogWarning($"[Lua] {msg}");
    public void LogError(string msg)   => Debug.LogError($"[Lua] {msg}");

    // ── C# 侧调用 ─────────────────────────────────────────────────────────────

    // 注册 C# 组件工厂（游戏初始化阶段调用，热重载后仍有效）
    [MoonSharpHidden]
    public void RegisterCSharpComponent(string name, Action<UnitEntity> factory)
    {
        _csharpFactories[name] = factory;
    }

    // 将命名组件应用到实体：
    //   - C# 工厂组件：直接调用工厂创建并注册到 entity
    //   - Lua 模板组件：实例化 Table 后挂载到 customComponent（可为 null，此时自动跳过 Lua 分支）
    [MoonSharpHidden]
    public void ApplyComponent(string name, UnitEntity entity, CustomComponent customComponent)
    {
        if (_csharpFactories.TryGetValue(name, out var factory))
        {
            factory(entity);
            return;
        }

        if (_templates.TryGetValue(name, out var template))
        {
            if (customComponent == null)
            {
                Debug.LogWarning($"[Lua] 应用 Lua 组件 '{name}' 失败：未提供 CustomComponent。");
                return;
            }

            Table instance = new Table(_script);
            foreach (var pair in template.Pairs)
                instance[pair.Key] = pair.Value;

            customComponent.AddComponent(instance);
            return;
        }

        Debug.LogWarning($"[Lua] 组件 '{name}' 未找到（既不是 C# 工厂，也不是 Lua 模板）。");
    }

    // 热重载时只清空 Lua 模板，C# 工厂由游戏代码负责，保持不变
    [MoonSharpHidden]
    public void ClearComponents() => _templates.Clear();

    [MoonSharpHidden]
    public IReadOnlyDictionary<string, Table> Templates => _templates;

    [MoonSharpHidden]
    public IReadOnlyDictionary<string, Action<UnitEntity>> CSharpFactories => _csharpFactories;
}
