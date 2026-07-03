using System.Collections.Generic;
using UnityEngine;
using XLua;

/// <summary>
/// 将 LuaTable 作为组件挂载到实体上，Unity 生命周期自动转发给所有 LuaTable。
///
/// 用法：
///   var luaComp = gameObject.AddComponent&lt;CustomComponent&gt;();
///   luaComp.AddComponent(myLuaTable);
///
/// LuaTable 只要实现了同名方法（Awake/Start/Update/OnDestroy 等），
/// 就会在对应时机被调用。
/// </summary>
public class CustomComponent : MonoBehaviour
{
    private readonly List<LuaTable> _components = new List<LuaTable>();

    public void AddComponent(LuaTable component)    => _components.Add(component);
    public void RemoveComponent(LuaTable component) => _components.Remove(component);

    // ── Unity 生命周期转发 ─────────────────────────────────────────────────────

    private void Awake()              => Dispatch("Awake");
    private void Start()              => Dispatch("Start");
    private void Update()             => Dispatch("Update");
    private void LateUpdate()         => Dispatch("LateUpdate");
    private void FixedUpdate()        => Dispatch("FixedUpdate");
    private void OnDestroy()          => Dispatch("OnDestroy");
    private void OnEnable()           => Dispatch("OnEnable");
    private void OnDisable()          => Dispatch("OnDisable");

    private void OnCollisionEnter2D(Collision2D col)      => Dispatch("OnCollisionEnter2D", col);
    private void OnCollisionStay2D(Collision2D col)       => Dispatch("OnCollisionStay2D", col);
    private void OnCollisionExit2D(Collision2D col)        => Dispatch("OnCollisionExit2D", col);
    private void OnTriggerEnter2D(Collider2D col)          => Dispatch("OnTriggerEnter2D", col);
    private void OnTriggerStay2D(Collider2D col)           => Dispatch("OnTriggerStay2D", col);
    private void OnTriggerExit2D(Collider2D col)           => Dispatch("OnTriggerExit2D", col);

    // ── 内部 ────────────────────────────────────────────────────────────────────

    private void Dispatch(string methodName, object arg = null)
    {
        var snapshot = _components.ToArray();   // 快照防止迭代中修改列表
        foreach (var comp in snapshot)
        {
            var fn = comp.Get<LuaFunction>(methodName);
            if (fn == null) continue;
            if (arg == null) fn.Call(comp);
            else             fn.Call(comp, arg);
        }
    }
}
