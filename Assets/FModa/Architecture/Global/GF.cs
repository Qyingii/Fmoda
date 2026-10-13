using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 全局数据,这里写一些全局可用的字段和方法
/// </summary>
public class GF : MonoBehaviour
{
    public static readonly float tick = 0.1f;//逻辑帧
    private float _tickCount = 0;//逻辑帧计数
    public static GF Instance;

    //全局字段
    public static GameConfig config;//游戏配置
    public static FlowManager flowManager;//流程管理器

    #region 生命周期函数

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        //初始化
        config = Resources.Load<GameConfig>("GameConfig");
        if (config == null)
        {
            Debug.LogWarning("没有找到 GameConfig，请在 Resources 下创建 GameConfig");
        }
        // DataManager 使用懒加载，首次访问时自动创建数据中心
        DataManager.PreWarm();

        //初始化流程管理器
        flowManager = new FlowManager();

        //扫描场景，自动注册已挂载在 GameObject 上的 IModule 组件（如 RAManager）
        AutoRegisterSceneModules();

        //启动第一个流程
        flowManager.StartFlow<ExampleFlow>();

    }

    void Start()
    {

    }

    private void Update()
    {
        if (_tickCount >= tick)
        {
            DispatchModuleUpdate();

            //流程更新
            flowManager.CurrentFlow.Update();
            
            //逻辑帧结束后触发模块的 OnLateUpdate
            DispatchModuleLateUpdate();
            _tickCount = 0;
        }
        else
        {
            _tickCount += Time.deltaTime;
        }
    }

    private void OnDestroy()
    {
        DispatchModuleDestroy();
    }

    private void OnApplicationQuit()
    {
        DispatchModuleQuit();
    }

    #endregion

    #region 模块管理

    /// <summary> 所有已注册的模块，key = 接口类型 </summary>
    private static readonly Dictionary<Type, object> _moduleMap = new Dictionary<Type, object>();

    /// <summary> 模块优先级（数字越小越先执行）</summary>
    private static readonly Dictionary<Type, int> _priorityMap = new Dictionary<Type, int>();

    /// <summary> 缓存脏标记 — 注册/移除后重建缓存 </summary>
    private static bool _cacheDirty = true;

    /// <summary> 具体类型 → 接口类型 反查（支持按具体类名 Get/Remove）</summary>
    private static readonly Dictionary<Type, Type> _concreteToInterface = new Dictionary<Type, Type>();

    // 各生命周期缓存（按优先级排序，已去重）
    private static List<IModule> _sortedModules;

    // ── 公开 API ──

    /// <summary>
    /// 注册一个已创建的模块实例。
    /// 按接口类型存储，外部只能通过接口访问。
    /// 若该接口已注册则跳过并返回已有实例。
    /// 模块需实现 <see cref="IModule"/>，否则打印警告。
    /// </summary>
    public static T RegisterModule<T>(T module, int priority = -1) where T : class
    {
        if (module == null)
        {
            Debug.LogError("[GF] RegisterModule 失败：module 为 null");
            return null;
        }
        return (T)RegisterModuleByType(typeof(T), module, priority);
    }

    /// <summary>
    /// 创建并注册模块（模块需实现 IModule 声明自己的接口类型）。
    /// 由模块的 <see cref="IModule.InterfaceType"/> 自动匹配接口。
    /// 若类型继承 MonoBehaviour，自动创建 GameObject 挂载。
    /// </summary>
    public static T CreateModule<T>(int priority = -1) where T : class, IModule, new()
    {
        var module = CreateModuleInstance<T>();
        return (T)RegisterModuleByType(module.InterfaceType, module, priority);
    }

    /// <summary>
    /// 获取已注册的模块，T 为实现了 IModule 的具体类型。
    /// 不存在时自动创建并注册。
    /// </summary>
    public static T GetModule<T>(int priority = -1) where T : class, IModule
    {
        // 1. 按反查表找到接口 key 再查（T 已注册过的具体类型）
        if (_concreteToInterface.TryGetValue(typeof(T), out var ifaceType))
        {
            if (_moduleMap.TryGetValue(ifaceType, out var module))
                return module as T;
        }

        // 2. 不存在 → 若 T 是实现了 IModule 的具体类型则自动创建
        if (typeof(IModule).IsAssignableFrom(typeof(T)))
        {
            var instance = CreateModuleInstance(typeof(T));
            if (instance is IModule imodule)
            {
                RegisterModuleByType(imodule.InterfaceType, instance, priority);
                return (T)instance;
            }
        }

        return null;
    }

    /// <summary>
    /// 检查模块是否已注册，T 为实现了 IModule 的具体类型。
    /// </summary>
    public static bool HasModule<T>() where T : class
    {
        return _concreteToInterface.ContainsKey(typeof(T));
    }

    /// <summary>
    /// 移除并销毁模块（触发 IModule.OnDestroy），T 为实现了 IModule 的具体类型。
    /// </summary>
    public static void RemoveModule<T>() where T : class
    {
        // 通过反查表找到接口 key
        if (!_concreteToInterface.TryGetValue(typeof(T), out var ifaceType))
            return;

        if (_moduleMap.TryGetValue(ifaceType, out var module))
        {
            if (module is IModule imod)
            {
                try { imod.OnDestroy(); }
                catch (Exception e) { Debug.LogError($"[GF] 模块 {ifaceType.Name} OnDestroy 异常: {e}"); }
            }

            // MonoBehaviour 模块 → 销毁其 GameObject
            DestroyModuleObject(module);

            _moduleMap.Remove(ifaceType);
            _priorityMap.Remove(ifaceType);
            _concreteToInterface.Remove(module.GetType());
            _cacheDirty = true;
            Debug.Log($"[GF] 移除模块: {ifaceType.Name}");
        }
    }

    /// <summary>
    /// 清除所有模块（场景切换时调用），逆序触发 IModule.OnDestroy。
    /// </summary>
    public static void ClearAllModules()
    {
        // 逆序销毁（先去重，避免同一对象被多个接口注册时重复销毁）
        var snapshot = _moduleMap.Values.Distinct().ToArray();
        for (int i = snapshot.Length - 1; i >= 0; i--)
        {
            if (snapshot[i] is IModule imod)
            {
                try { imod.OnDestroy(); }
                catch (Exception e) { Debug.LogError($"[GF] Destroy 异常: {e}"); }
            }
            DestroyModuleObject(snapshot[i]);
        }

        _moduleMap.Clear();
        _priorityMap.Clear();
        _concreteToInterface.Clear();
        _cacheDirty = true;
        Debug.Log("[GF] 已清除所有模块");
    }

    // ── 内部：场景扫描 ──

    /// <summary>
    /// 扫描场景中所有挂载了 IModule 的 MonoBehaviour，自动注册到模块系统。
    /// 这样用户只需在场景中拖好组件、绑好引用，无需手动调用 RegisterModule。
    /// </summary>
    private static void AutoRegisterSceneModules()
    {
        // 注意：不能直接用 FindObjectsByType<IModule>，接口不能作为 Unity 类型参数
        var allMBs = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.InstanceID);
        foreach (var mb in allMBs)
        {
            if (mb == null) continue;
            if (mb is IModule module && !_moduleMap.ContainsKey(module.InterfaceType))
            {
                Debug.Log($"[GF] 自动注册场景模块: {module.InterfaceType.Name} (挂载在 {mb.gameObject.name})");
                RegisterModuleByType(module.InterfaceType, mb, module.Priority);
            }
        }
    }

    // ── 内部：创建模块实例（自动处理 MonoBehaviour 挂载）──

    /// <summary>
    /// 创建模块实例。若 T 继承 MonoBehaviour，自动创建 GameObject 并挂载。
    /// </summary>
    private static T CreateModuleInstance<T>() where T : class, IModule, new()
    {
        if (typeof(MonoBehaviour).IsAssignableFrom(typeof(T)))
        {
            var go = new GameObject(typeof(T).Name);
            if (Instance != null)
                go.transform.SetParent(Instance.transform);
            return (T)(object)go.AddComponent(typeof(T));
        }
        return new T();
    }

    /// <summary>
    /// 按运行时类型创建模块实例（供反射路径使用）。
    /// </summary>
    private static object CreateModuleInstance(Type type)
    {
        if (typeof(MonoBehaviour).IsAssignableFrom(type))
        {
            var go = new GameObject(type.Name);
            if (Instance != null)
                go.transform.SetParent(Instance.transform);
            return go.AddComponent(type);
        }
        var ctor = type.GetConstructor(Type.EmptyTypes);
        if (ctor != null)
            return ctor.Invoke(null);
        Debug.LogError($"[GF] 类型 {type.Name} 没有无参构造，无法自动创建");
        return null;
    }

    /// <summary> 若模块是 MonoBehaviour 则销毁其 GameObject </summary>
    private static void DestroyModuleObject(object module)
    {
        if (module is MonoBehaviour mb && mb.gameObject != null)
        {
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(mb.gameObject);
            else
                UnityEngine.Object.DestroyImmediate(mb.gameObject);
        }
    }

    // ── 内部：按类型注册（公共入口）──

    /// <summary>
    /// 按运行时接口类型注册模块，不依赖泛型约束。
    /// RegisterModule / CreateModule 最终都走这里。
    /// </summary>
    private static object RegisterModuleByType(Type interfaceType, object module, int priority)
    {
        if (_moduleMap.TryGetValue(interfaceType, out var existing))
        {
            Debug.LogWarning($"[GF] 模块 {interfaceType.Name} 已注册，跳过");
            return existing;
        }

        // 所有模块必须实现 IModule，否则运行时会警告
        if (!(module is IModule))
        {
            Debug.LogWarning($"[GF] 模块 {interfaceType.Name} 未实现 IModule，将跳过生命周期管理，" +
                             $"建议实现 IModule.InterfaceType");
        }

        if(priority < 0) priority = (module as IModule)?.Priority ?? 5; // 默认优先级 5
        _moduleMap[interfaceType] = module;
        _priorityMap[interfaceType] = priority;
        _cacheDirty = true;

        // 记录反查：具体类型 → 接口类型，支持 GetModule<MyConcrete>() 语法
        _concreteToInterface[module.GetType()] = interfaceType;

        // 注册时触发初始化（IModule 自带默认空实现，未重写也不报错）
        if (module is IModule im)
        {
            try { im.OnAwake(); } catch (Exception e) { Debug.LogError($"[GF] 模块 {interfaceType.Name} OnAwake 异常: {e}"); }
            try { im.OnStart(); } catch (Exception e) { Debug.LogError($"[GF] 模块 {interfaceType.Name} OnStart 异常: {e}"); }
        }

        Debug.Log($"[GF] 注册模块: {interfaceType.Name} (priority={priority})");
        return module;
    }

    // ── 内部：缓存重建 ──

    private static void RebuildCache()
    {
        if (!_cacheDirty) return;

        // Values 可能含同一对象（不同接口），按引用去重
        var distinct = _moduleMap.Values.Distinct().ToList();

        int GetPriority(object m)
        {
            // 用对象拿它所有注册接口中最低的 priority
            int minP = int.MaxValue;
            foreach (var kv in _priorityMap)
            {
                if (ReferenceEquals(_moduleMap[kv.Key], m) && kv.Value < minP)
                    minP = kv.Value;
            }
            return minP == int.MaxValue ? 0 : minP;
        }

        // 生命周期方法已合入 IModule 接口（默认空实现），不再按接口过滤
        _sortedModules = distinct.Cast<IModule>().OrderBy(GetPriority).ToList();

        _cacheDirty = false;
    }

    // ── 生命周期派发 ──

    /// <summary> 安全遍历列表，每个元素包裹 try-catch </summary>
    private static void SafeCall(List<IModule> list, Action<IModule> fn)
    {
        if (list == null) return;
        foreach (var item in list)
        {
            try { fn(item); }
            catch (Exception e) { Debug.LogError($"[GF] 模块 {item.GetType().Name} 异常: {e}"); }
        }
    }

    private void DispatchModuleUpdate()
    {
        RebuildCache();
        SafeCall(_sortedModules, m => m.OnUpdate());
    }

    private void DispatchModuleLateUpdate()
    {
        RebuildCache();
        SafeCall(_sortedModules, m => m.OnLateUpdate());
    }

    private void DispatchModuleDestroy()
    {
        RebuildCache();
        if (_sortedModules == null) return;
        // 逆序销毁（依赖方先销毁）
        for (int i = _sortedModules.Count - 1; i >= 0; i--)
        {
            try { _sortedModules[i].OnDestroy(); }
            catch (Exception e) { Debug.LogError($"[GF] 模块 {_sortedModules[i].GetType().Name} OnDestroy 异常: {e}"); }
        }
    }

    private void DispatchModuleQuit()
    {
        RebuildCache();
        SafeCall(_sortedModules, m => m.OnApplicationQuit());
    }

    #endregion
}