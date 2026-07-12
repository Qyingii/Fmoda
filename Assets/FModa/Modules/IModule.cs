using System;

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

    /// <summary>
    /// 初始调用时触发
    /// </summary>
    void OnAwake(){}

    /// <summary>
    /// 在Awake之后、Update之前调用
    /// </summary>
    void OnStart(){}

    /// <summary>
    /// 每帧调用
    /// </summary>
    void OnUpdate(){}

    /// <summary>
    /// 每帧调用，在Update之后
    /// </summary>
    void OnLateUpdate(){}

    /// <summary>
    /// 销毁时调用
    /// </summary>
    void OnDestroy(){}

    /// <summary>
    /// 应用退出时调用
    /// </summary>
    void OnApplicationQuit(){}


}
