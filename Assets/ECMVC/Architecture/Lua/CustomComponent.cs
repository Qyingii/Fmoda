using System.Collections.Generic;
using MoonSharp.Interpreter;

// 将 MoonSharp Lua Table 作为组件挂载到实体上，
// 实现不重新编译 C# 即可扩展实体行为的热更新能力。
public class CustomComponent : ComponentBehaviour,
    IAwake, IStart, IUpdate, ILateUpdate, IFixedUpdate,
    IOnDestroy, IOnEnable, IOnDisable,
    IOnCollisionEnter2D, IOnCollisionStay2D, IOnCollisionExit2D,
    IOnTriggerEnter2D, IOnTriggerStay2D, IOnTriggerExit2D
{
    private readonly List<Table> _components = new List<Table>();
    public List<Table> Components => _components;

    public CustomComponent(UnitEntity entityBehaviour) : base(entityBehaviour) { }

    public void AddComponent(Table component)    => _components.Add(component);
    public void RemoveComponent(Table component) => _components.Remove(component);

    public void Awake()              => Dispatch("Awake");
    public void Start()              => Dispatch("Start");
    public void Update()             => Dispatch("Update");
    public void LateUpdate()         => Dispatch("LateUpdate");
    public void FixedUpdate()        => Dispatch("FixedUpdate");
    public void OnDestroy()          => Dispatch("OnDestroy");
    public void OnEnable()           => Dispatch("OnEnable");
    public void OnDisable()          => Dispatch("OnDisable");

    public void OnCollisionEnter2D(UnityEngine.Collision2D col) => Dispatch("OnCollisionEnter2D", col);
    public void OnCollisionStay2D(UnityEngine.Collision2D col)  => Dispatch("OnCollisionStay2D",  col);
    public void OnCollisionExit2D(UnityEngine.Collision2D col)  => Dispatch("OnCollisionExit2D",  col);
    public void OnTriggerEnter2D(UnityEngine.Collider2D col)    => Dispatch("OnTriggerEnter2D",   col);
    public void OnTriggerStay2D(UnityEngine.Collider2D col)     => Dispatch("OnTriggerStay2D",    col);
    public void OnTriggerExit2D(UnityEngine.Collider2D col)     => Dispatch("OnTriggerExit2D",    col);

    private void Dispatch(string methodName, object arg = null)
    {
        var snapshot = _components.ToArray();
        foreach (var comp in snapshot)
        {
            var fn = comp.Get(methodName);
            if (fn.Type != DataType.Function) continue;
            if (arg == null) fn.Function.Call(comp);
            else             fn.Function.Call(comp, arg);
        }
    }
}
