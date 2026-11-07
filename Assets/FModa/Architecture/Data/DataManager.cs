using Conquer.Unit;
using FModa.Multilingual;

namespace FModa.Data
{
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
    ///   // 新增一个数据中心：先在 DataCenterCodes 占号，再在这里加属性
    ///   private static InventoryDC _inventory;
    ///   public static InventoryDC Inventory => _inventory ??= new InventoryDC(DataCenterCodes.Inventory);
    ///   </code>
    /// </summary>
    public static class DataManager
    {
        //单位数据中心
        private static UnitDataCenter _unit;
        public static UnitDataCenter Unit => _unit ??= new UnitDataCenter(DataCenterCodes.Unit);

        //多语言数据中心
        private static MultilingualDataCenter _multilingual;
        public static MultilingualDataCenter Multilingual => _multilingual ??= new MultilingualDataCenter(DataCenterCodes.Multilingual);

        // ── 预加载（可选）──

        /// <summary>
        /// 提前初始化指定的数据中心（非必须，不调用也会在首次访问时自动创建）。
        /// 需要在游戏启动时就准备好的数据在这里 new。
        /// </summary>
        public static void PreWarm()
        {
            // 例：战斗系统在游戏一开始就会用到
            _unit = new UnitDataCenter(DataCenterCodes.Unit);
            _multilingual = new MultilingualDataCenter(DataCenterCodes.Multilingual);

            // 后续加：
            // _player = new PlayerDC(DataCenterCodes.Player);
        }
    }

    /// <summary>
    /// 数据中心 Code —— 显式、稳定、只增不改。
    ///
    /// 一个 Code 对应一种数据中心类型；一旦发布到存档或网络，号码不得复用、
    /// 不得调整顺序，也不得换绑到另一种数据中心类型。
    ///
    /// 新增数据中心时先在这里占号，再在 DataManager 中注册属性。
    /// 没有占号的数据中心使用 Code 0，id 不做跨中心隔离（与旧版本行为一致）。
    /// </summary>
    public static class DataCenterCodes
    {
        //数据最终ID由数据中心ID+数据ID组成，通过代号可以知道一个ID是不是这个数据中心的数据
        //代号从1开始，0代表独立，不进行Id审查
        public const int Unit = 1;
        public const int Multilingual = 2;
    }
}


