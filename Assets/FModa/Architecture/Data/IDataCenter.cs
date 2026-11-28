using FModa.Data;

/// <summary>
/// 数据中心契约。具体中心继承 <see cref="DataCenter{T}"/>，只补自己的语义方法。
///
/// 创建与访问：
/// 1. 数据中心提供公共无参构造，并继承 DataCenter&lt;T&gt;。
/// 2. 模块通过 DataManager.Get&lt;PlayerDataCenter&gt;() 获取；首次获取会自动调用
///    Init 分配当前运行时的 CenterCode，无需再维护 DataCenterCodes 或 CreateRegistry。
/// 3. 只有模块可以访问数据中心；Render / UI 一律通过模块。
///
/// id 是运行时句柄：存档保存 sourceId 供导入重建引用；Replace 和 Append 都重新发号。
/// 基类没有 OnDataAdded / OnDataRemoved / OnCleared，需要通知时在子类或模块中显式定义。
/// </summary>
public interface IDataCenter
{
    /// <summary>当前运行时的中心 Code。</summary>
    int CenterCode { get; }

    /// <summary>由 DataManager 在注册或创建候选实例时调用一次。</summary>
    void Init(int centerCode);
}
