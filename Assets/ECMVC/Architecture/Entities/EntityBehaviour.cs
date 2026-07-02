using System;
using System.Collections.Generic;
using UnityEngine;

public class EntityBehaviour : MonoBehaviour
{
    #region 生命周期列表

    private IUpdate[]      cachedUpdates      = Array.Empty<IUpdate>();
    private IFixedUpdate[] cachedFixedUpdates = Array.Empty<IFixedUpdate>();
    private ILateUpdate[]  cachedLateUpdates  = Array.Empty<ILateUpdate>();

    private readonly List<IUpdate>      updates      = new List<IUpdate>();
    private readonly List<IFixedUpdate> fixedUpdates = new List<IFixedUpdate>();
    private readonly List<ILateUpdate>  lateUpdates  = new List<ILateUpdate>();

    private bool updatesFlag      = false;
    private bool fixedUpdatesFlag = false;
    private bool lateUpdatesFlag  = false;

    private readonly List<IAwake>     awakes     = new List<IAwake>();
    private readonly List<IStart>     starts     = new List<IStart>();
    private readonly List<IOnDestroy> onDestroys = new List<IOnDestroy>();
    private readonly List<IOnEnable>  onEnables  = new List<IOnEnable>();
    private readonly List<IOnDisable> onDisables = new List<IOnDisable>();

    private List<IOnCollisionEnter2D>   m_onCollisionEnters2D;
    private List<IOnTriggerEnter2D>     m_onTriggerEnters2D;
    private List<IOnCollisionExit2D>    m_onCollisionExits2D;
    private List<IOnTriggerExit2D>      m_onTriggerExits2D;
    private List<IOnCollisionStay2D>    m_onCollisionStays2D;
    private List<IOnTriggerStay2D>      m_onTriggerStays2D;
    private List<IOnGUI>                m_onGUIs;
    private List<IOnDrawGizmos>         m_onDrawGizmoses;
    private List<IOnDrawGizmosSelected> m_onDrawGizmosSelecteds;

    #endregion

    #region 组件存储

    private readonly Dictionary<Type, ComponentBehaviour> components = new Dictionary<Type, ComponentBehaviour>();

    #endregion

    #region 实体关系

    private readonly List<EntityBehaviour> childEntities = new List<EntityBehaviour>();
    private EntityBehaviour m_parentEntity;

    public EntityBehaviour parentEntity
    {
        get => m_parentEntity;
        set => m_parentEntity = value;
    }

    #endregion

    #region 标签

    private readonly HashSet<string> m_tags = new HashSet<string>();

    public bool HasTag(string tag)    => m_tags.Contains(tag);
    public bool AddTag(string tag)    => m_tags.Add(tag);
    public bool RemoveTag(string tag) => m_tags.Remove(tag);

    // 由子类在 Awake 中传入序列化字符串（如 "Enemy,Ground"）完成初始化
    protected void InitTags(string tagString)
    {
        if (string.IsNullOrEmpty(tagString)) return;
        foreach (var t in tagString.Split(','))
        {
            var trimmed = t.Trim();
            if (!string.IsNullOrEmpty(trimmed))
                m_tags.Add(trimmed);
        }
    }

    #endregion

    #region Unity 生命周期分发

    protected virtual void Awake()
    {
        for (int i = 0; i < awakes.Count; i++) awakes[i].Awake();
    }

    private void Start()
    {
        for (int i = 0; i < starts.Count; i++) starts[i].Start();
    }

    private void Update()
    {
        if (!updatesFlag) return;
        var arr = cachedUpdates;
        for (int i = 0; i < arr.Length; i++) arr[i].Update();
    }

    private void FixedUpdate()
    {
        if (!fixedUpdatesFlag) return;
        var arr = cachedFixedUpdates;
        for (int i = 0; i < arr.Length; i++) arr[i].FixedUpdate();
    }

    private void LateUpdate()
    {
        if (!lateUpdatesFlag) return;
        var arr = cachedLateUpdates;
        for (int i = 0; i < arr.Length; i++) arr[i].LateUpdate();
    }

    private void OnDestroy()
    {
        for (int i = 0; i < onDestroys.Count; i++) onDestroys[i].OnDestroy();
    }

    private void OnEnable()
    {
        for (int i = 0; i < onEnables.Count; i++) onEnables[i].OnEnable();
    }

    private void OnDisable()
    {
        for (int i = 0; i < onDisables.Count; i++) onDisables[i].OnDisable();
    }

    private void OnCollisionEnter2D(Collision2D col)
    {
        if (m_onCollisionEnters2D == null) return;
        for (int i = 0; i < m_onCollisionEnters2D.Count; i++) m_onCollisionEnters2D[i].OnCollisionEnter2D(col);
    }

    private void OnCollisionStay2D(Collision2D col)
    {
        if (m_onCollisionStays2D == null) return;
        for (int i = 0; i < m_onCollisionStays2D.Count; i++) m_onCollisionStays2D[i].OnCollisionStay2D(col);
    }

    private void OnCollisionExit2D(Collision2D col)
    {
        if (m_onCollisionExits2D == null) return;
        for (int i = 0; i < m_onCollisionExits2D.Count; i++) m_onCollisionExits2D[i].OnCollisionExit2D(col);
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        if (m_onTriggerEnters2D == null) return;
        for (int i = 0; i < m_onTriggerEnters2D.Count; i++) m_onTriggerEnters2D[i].OnTriggerEnter2D(col);
    }

    private void OnTriggerStay2D(Collider2D col)
    {
        if (m_onTriggerStays2D == null) return;
        for (int i = 0; i < m_onTriggerStays2D.Count; i++) m_onTriggerStays2D[i].OnTriggerStay2D(col);
    }

    private void OnTriggerExit2D(Collider2D col)
    {
        if (m_onTriggerExits2D == null) return;
        for (int i = 0; i < m_onTriggerExits2D.Count; i++) m_onTriggerExits2D[i].OnTriggerExit2D(col);
    }

    private void OnGUI()
    {
        if (m_onGUIs == null) return;
        for (int i = 0; i < m_onGUIs.Count; i++) m_onGUIs[i].OnGUI();
    }

    private void OnDrawGizmos()
    {
        if (m_onDrawGizmoses == null) return;
        for (int i = 0; i < m_onDrawGizmoses.Count; i++) m_onDrawGizmoses[i].OnDrawGizmos();
    }

    private void OnDrawGizmosSelected()
    {
        if (m_onDrawGizmosSelecteds == null) return;
        for (int i = 0; i < m_onDrawGizmosSelecteds.Count; i++) m_onDrawGizmosSelecteds[i].OnDrawGizmosSelected();
    }

    #endregion

    #region 生命周期注册

    private void UpdateLifecycleLists(ComponentBehaviour component, bool adding)
    {
        TryRegisterHotPath(component as IUpdate,     updates,      ref cachedUpdates,      ref updatesFlag,      adding);
        TryRegisterHotPath(component as IFixedUpdate, fixedUpdates, ref cachedFixedUpdates, ref fixedUpdatesFlag, adding);
        TryRegisterHotPath(component as ILateUpdate,  lateUpdates,  ref cachedLateUpdates,  ref lateUpdatesFlag,  adding);

        TryRegister(component as IAwake,     awakes,     adding);
        TryRegister(component as IStart,     starts,     adding);
        TryRegister(component as IOnDestroy, onDestroys, adding);
        TryRegister(component as IOnEnable,  onEnables,  adding);
        TryRegister(component as IOnDisable, onDisables, adding);

        TryRegisterLazy(component as IOnCollisionEnter2D,   ref m_onCollisionEnters2D,   adding);
        TryRegisterLazy(component as IOnTriggerEnter2D,     ref m_onTriggerEnters2D,     adding);
        TryRegisterLazy(component as IOnCollisionExit2D,    ref m_onCollisionExits2D,    adding);
        TryRegisterLazy(component as IOnTriggerExit2D,      ref m_onTriggerExits2D,      adding);
        TryRegisterLazy(component as IOnCollisionStay2D,    ref m_onCollisionStays2D,    adding);
        TryRegisterLazy(component as IOnTriggerStay2D,      ref m_onTriggerStays2D,      adding);
        TryRegisterLazy(component as IOnGUI,                ref m_onGUIs,                adding);
        TryRegisterLazy(component as IOnDrawGizmos,         ref m_onDrawGizmoses,        adding);
        TryRegisterLazy(component as IOnDrawGizmosSelected, ref m_onDrawGizmosSelecteds, adding);
    }

    // Update/FixedUpdate/LateUpdate 需要额外维护数组缓存和非空标志
    private void TryRegisterHotPath<T>(T item, List<T> list, ref T[] cache, ref bool flag, bool adding) where T : class
    {
        if (item == null) return;
        if (adding) { list.Add(item); flag = true; }
        else        { list.Remove(item); if (list.Count == 0) flag = false; }
        cache = list.ToArray();
    }

    private static void TryRegister<T>(T item, List<T> list, bool adding) where T : class
    {
        if (item == null) return;
        if (adding) list.Add(item);
        else        list.Remove(item);
    }

    // 低频事件列表按需创建，不占用无关实体的内存
    private static void TryRegisterLazy<T>(T item, ref List<T> list, bool adding) where T : class
    {
        if (item == null) return;
        if (adding) (list ??= new List<T>()).Add(item);
        else        list?.Remove(item);
    }

    #endregion

    #region 组件管理

    public void AddComponentQ<T>(T component) where T : ComponentBehaviour
    {
        if (components.ContainsKey(typeof(T)))
        {
            Debug.LogWarning($"Component of type {typeof(T)} already exists on entity.");
            return;
        }
        components.Add(typeof(T), component);
        try
        {
            component.Add();
            UpdateLifecycleLists(component, true);
        }
        catch
        {
            components.Remove(typeof(T));
            throw;
        }
    }

    // 运行时类型版本，供 Lua 工厂注册模式使用（无法在编译期确定泛型 T 时）
    public void AddComponentQDynamic(ComponentBehaviour component)
    {
        var type = component.GetType();
        if (components.ContainsKey(type))
        {
            Debug.LogWarning($"Component of type {type} already exists on entity.");
            return;
        }
        components.Add(type, component);
        try
        {
            component.Add();
            UpdateLifecycleLists(component, true);
        }
        catch
        {
            components.Remove(type);
            throw;
        }
    }

    public T GetComponentQ<T>() where T : ComponentBehaviour
    {
        return components.TryGetValue(typeof(T), out var component) ? (T)component : null;
    }

    public void RemoveComponentQ<T>() where T : ComponentBehaviour
    {
        if (!components.TryGetValue(typeof(T), out var component))
        {
            Debug.LogWarning($"Component of type {typeof(T)} does not exist on entity.");
            return;
        }
        component.Remove();
        components.Remove(typeof(T));
        UpdateLifecycleLists(component, false);
    }

    public bool HasComponentQ<T>() where T : ComponentBehaviour
    {
        return components.ContainsKey(typeof(T));
    }

    #endregion

    #region 子实体管理

    // 遍历场景层级并刷新缓存，为每个子实体设置其直接父实体引用
    public List<EntityBehaviour> GetAllChildEntities()
    {
        childEntities.Clear();
        var found = GetComponentsInChildren<EntityBehaviour>();
        foreach (var child in found)
        {
            if (child == this) continue; // GetComponentsInChildren 包含自身，需跳过
            childEntities.Add(child);
            child.parentEntity = child.transform.parent
                ?.GetComponentInParent<EntityBehaviour>();
        }
        return childEntities;
    }

    public void AddChildEntity(EntityBehaviour child)
    {
        child.parentEntity = this;
        childEntities.Add(child);
    }

    public void RemoveChildEntity(EntityBehaviour child)
    {
        child.parentEntity = null;
        childEntities.Remove(child);
    }
    public List<EntityBehaviour> GetChildEntities()      => childEntities;

    // 返回 true 表示 child 的直接父实体是 this
    public bool IsParentOf(EntityBehaviour child) => child.parentEntity == this;

    #endregion
}
