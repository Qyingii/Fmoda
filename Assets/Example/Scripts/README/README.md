## 示例职责分布

本示例对应的 Bond / Flow / PM 职责边界如下。`ExampleFlow.cs` 直接创建和移除示例模块，这是允许的单模块管理；涉及多个模块或模块与 Render / UI 联动的步骤仍由 Bond 完成：

- `Flows/ExampleFlow.cs`：直接管理示例模块，排列进入、退出和流程切换步骤，不在流程里实现多模块协作、单位生成或视图联动。
- `Bond/Unit/UnitBond.cs`：组合 `UnitManager`、单位 Render、`GF.Spawn` 和 `UnitHpPanel`，提供可复用的 `SpawnUnit` / `CreateUnitData` 步骤。
- `Bond/SwitchFlow/SwitchFlowBond.cs`：只广播结束流程请求；真正的 `GF.flow.SwitchFlow<EndFlow>(this)` 仍由 Flow 实现。
- `Modules/Unit/`：保存单位业务规则和数据访问。
- `Renders/Unit/Units.cs`、`UI/UnitHpPanel.cs`：负责表现与输入转发，不直接访问 `DataManager`。
- `Global/PM.cs`：只放与模块无关的纯静态工具方法，本示例的单位生成与转换逻辑不属于 PM。

## 播放时序

点击播放后，主要时序如下。

### Awake 阶段

1. `GF.Awake()` 加载 `GameConfig`，预热线数据中心。
2. GF 自动注册场景中挂载的模块。
3. GF 获取 `UIManager`、创建对象池并初始化 `GameObjectManager`。
4. GF 启动第一个 Flow。
5. Flow 按顺序取得所需 Bond，并调用 `UnitBond.SpawnUnit(...)`。
6. `UnitBond` 生成圆形单位、注册 `UnitData`、把 id 注入 `Units`，再触发 `UnitHpPanel` 刷新。

`GameObjectManager.Awake()` 还会扫描场景对象；GF 的默认执行顺序为 `-1000`，实际对象收录以 `sgo.Init()` 和场景加载扫描为准。

### OnEnable / Start 阶段

- `UnitHpPanel.OnEnable()` 驱动 `OnBind → OnRefresh`，订阅模块事件并做一次初始同步。
- `Units.Start()` 获取单位与输入模块。如果生成方没有注入 id，则通过 `UnitBond.CreateUnitData()` 执行静态场景单位的兜底注册。

因此，场景静态单位与 Bond 动态生成单位的注册时机不同，UI 初始刷新次数也可能不同。需要调整显示时，应修改 Bond 中可复用的生成／刷新步骤或 UI 自身绑定时机，**不要在 Flow 中展开涉及模块与 Render / UI 联动的注册、生成和刷新细节**。

## 框架用法

完整说明见 `Assets/FModa/README/README.md`，架构硬规则见 `Assets/FModa/Architecture/Skills/README.md`。
