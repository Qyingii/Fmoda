using System;

// ═══════════════════════════════════════════════════════════════════
//  模块生命周期接口 —— 模块按需实现即可，框架自动派发
//  命名加 On 前缀避免与 MonoBehaviour 同名方法冲突
// ═══════════════════════════════════════════════════════════════════

/// <summary> Awake 阶段 </summary>
public interface IModuleAwake { void OnAwake(); }

/// <summary> Start 阶段 </summary>
public interface IModuleStart { void OnStart(); }

/// <summary> Update 阶段（逻辑帧 Tick 内）</summary>
public interface IModuleUpdate { void OnUpdate(); }

/// <summary> FixedUpdate 阶段 </summary>
public interface IModuleFixedUpdate { void OnFixedUpdate(); }

/// <summary> LateUpdate 阶段 </summary>
public interface IModuleLateUpdate { void OnLateUpdate(); }

/// <summary> 销毁时 </summary>
public interface IModuleDestroy { void OnDestroy(); }

/// <summary> 应用退出时 </summary>
public interface IModuleQuit { void OnApplicationQuit(); }

/// <summary>
/// 模块标记接口 —— 模块实现此接口并声明 <see cref="InterfaceType"/>，
/// 即可用 <c>GF.CreateModule&lt;T&gt;()</c> 一键注册，无需手动指定接口类型。
/// </summary>
public interface IModule
{
    /// <summary> 此模块对外暴露的接口类型（例如 typeof(IMyInterface)）</summary>
    Type InterfaceType { get; }
    /// <summary>
    /// 模块优先级（数字越小越先执行），默认 5。
    /// </summary>
    int Priority { get; }
}
