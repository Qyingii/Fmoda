using Conquer.Unit;


/// <summary>
/// 数据管理器 —— 所有数据中心的统一注册入口。
/// 设计原则：
///   - 懒加载：数据中心在第一次被访问时自动创建，不会一开始就全部实例化
///   - 统一入口：所有数据中心通过 DataManager.xxx 访问，不分散到各个模块
///   - 职责单一：只管理数据中心生命周期，与模块系统解耦
/// 使用方式：
///   <code>
///   // 直接访问，按需自动创建
///   DataManager.Combat.SetHp(id, 50);
///
///   // 新增一个数据中心只需加一个属性：
///   private static InventoryDC _inventory;
///   public static InventoryDC Inventory => _inventory ??= new InventoryDC();
///   </code>
/// </summary>
public static class DataManager
{
    // ── 泛型数据表（冷数据/配置）──

    private static GenericTable _generic;
    public static GenericTable Generic => _generic ??= new GenericTable();

    //单位数据中心
    private static UnitDataCenter _unit;
    public static UnitDataCenter Unit => _unit ??= new UnitDataCenter();

    // ── 预加载（可选）──

    /// <summary>
    /// 提前初始化指定的数据中心（非必须，不调用也会在首次访问时自动创建）。
    /// 需要在游戏启动时就准备好的数据在这里 new。
    /// </summary>
    public static void PreWarm()
    {
        // 例：战斗系统在游戏一开始就会用到
        _unit = new UnitDataCenter();

        // 后续加：
        // _player = new PlayerDC();
    }

    /// <summary> 清理所有数据（场景切换时调用）</summary>
    public static void ClearAll()
    {
        _generic?.ClearAll();
        _unit?.Clear();
        // 子类各自实现 Clear
    }
}

