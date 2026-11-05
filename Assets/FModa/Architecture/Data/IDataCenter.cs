using System.Collections.Generic;
using FModa.Data; // 仅供下方 <see cref="DataCenter{T}"/> 解析

/// <summary>
/// 数据中心标记接口 —— 所有数据中心实现此接口。
///
/// 它本身不强制任何成员，真正的存储和事件都在泛型基类 <see cref="DataCenter{T}"/> 里。
///
/// ══════════════════════════════════════════════════════════════
///   如何创建一个数据中心
/// ══════════════════════════════════════════════════════════════
///
/// 1. 继承 <see cref="DataCenter{T}"/>，只补自己的语义方法：
///
///    <code>
///    public class PlayerDC : DataCenter&lt;PlayerData&gt;, IDataCenter
///    {
///        public void SetHp(long id, int newHp)
///        {
///            if (!datas.TryGetValue(id, out var data)) return; // datas 是基类的 protected 字段
///
///            int old = data.hp;
///            data.hp = newHp;
///
///            OnHpChanged?.Invoke(id, old, newHp);
///        }
///
///        public event Action&lt;long, int, int&gt; OnHpChanged; // (id, old, new)
///    }
///    </code>
///
///    增删改查、合成 id（数据中心 Code + 本地序号）、TryGetData / HasId 都由基类提供。
///    基类**没有** OnDataAdded / OnDataRemoved / OnCleared 事件；
///    需要通知就在子类里显式定义事件，并在语义方法里广播。
///
///    Id 规则见 <see cref="DataCenterId"/>：Code 在 DataManager.DataCenterCodes 中占号，
///    Code 0 表示未分区（旧行为）。只有 Id == -1 的新数据能 AddData。
///
/// 2. 在 DataManager 中注册（懒加载属性，首次访问时自动创建）：
///
///    <code>
///    // DataManager.cs
///    private static PlayerDC _player;
///    public static PlayerDC Player =&gt; _player ??= new PlayerDC();
///    </code>
///
/// 3. 在模块中访问（渲染器和 UI 不直接碰数据中心，一律通过模块）：
///
///    <code>
///    DataManager.Player.SetHp(id, 50);  // 类型安全，独立事件，零过滤
///    </code>
///
/// ══════════════════════════════════════════════════════════════
///   什么数据适合放进数据中心
/// ══════════════════════════════════════════════════════════════
///
///   | 场景                         | 方案                                 |
///   |------------------------------|--------------------------------------|
///   | 战斗、背包等高频变更数据     | 继承 DataCenter&lt;T&gt;，自有 typed 事件 |
///   | 需要复杂查询（按等级/类型）  | 继承 DataCenter&lt;T&gt;，自有查询方法   |
///   | 纯配置表、静态只读数据       | 不放数据中心，用 ScriptableObject    |
///
/// 具体可参考 Assets/Example/Scripts/Modules/Unit/Data/UnitDataCenter.cs
///
/// ══════════════════════════════════════════════════════════════
/// </summary>
public interface IDataCenter
{
    // IDataCenter 是一个标记接口，不强制任何成员。
    // 实现类可自行决定暴露什么方法和事件。
}
