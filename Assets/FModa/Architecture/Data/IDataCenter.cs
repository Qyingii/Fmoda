using System.Collections.Generic;

/// <summary>
/// 数据中心接口 —— 所有数据中心（typed 子类 / GenericTable）实现此接口。
///
/// ══════════════════════════════════════════════════════════════
///   如何创建一个 typed 数据中心
/// ══════════════════════════════════════════════════════════════
///
/// 1. 实现 IDataCenter，定义私有字典 + typed 事件：
///
///    <code>
///    public class PlayerDC : IDataCenter
///    {
///        private Dictionary&lt;int, PlayerData&gt; _players = new();
///        private int _nextId = 1;
///
///        public event Action&lt;int, int, int&gt; OnHpChanged; // (id, old, new)
///
///        public int AddPlayer(PlayerData data) { ... }
///
///        public void SetHp(int id, int newHp)
///        {
///            if (!_players.TryGetValue(id, out var data)) return;
///            int old = data.hp; data.hp = newHp;
///            OnHpChanged?.Invoke(id, old, newHp);
///        }
///    }
///    </code>
///
/// 2. 在 DataManager 中注册（懒加载属性，首次访问时自动创建）：
///
///    <code>
///    // DataManager.cs
///    public static PlayerDC Player;
///    Player = new PlayerDC();
///    </code>
///
/// 3. 在 System 中访问：
///
///    <code>
///    DataManager.Player.SetHp(id, 50);  // 类型安全，独立事件，零过滤
///    </code>
///
/// ══════════════════════════════════════════════════════════════
///   何时建 typed 子类，何时用 GenericTable
/// ══════════════════════════════════════════════════════════════
///
///   | 场景                         | 方案                           |
///   |------------------------------|--------------------------------|
///   | 战斗、背包等高频变更数据     | 实现 IDataCenter 建 typed 子类  |
///   | 配置表、静态只读数据         | DataManager.Generic 泛型 API    |
///   | 不确定未来会不会高频         | 先用 GenericTable               |
///   | 需要复杂查询（按等级/类型）  | typed 子类，自有查询方法        |
///
/// ══════════════════════════════════════════════════════════════
/// </summary>
public interface IDataCenter
{
    // IDataCenter 是一个标记接口，不强制任何成员。
    // 实现类可自行决定暴露什么方法和事件。
}
