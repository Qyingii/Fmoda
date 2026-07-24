# FModa

**F**low + **Mo**dule + **Da**ta — 一套面向 Unity 的轻量模块化框架。
- 使用说明参考：`Assets\Example\README\README.md`

---

## 核心概念

```
Flow（流程驱动） → Module（模块编排） → Data（数据中心）
                           ↕
                        UIWidget（响应式 UI）
```

| 层 | 组件 | 职责 |
|---|---|---|
| **Flow** | `FlowBase` / `FlowManager` | 游戏状态/流程切换（如 主菜单→战斗→结算） |
| **Module** | `IModule` | 业务逻辑编排，通过 GF 注册后自动接收生命周期 |
| **Data** | `DataManager` / `IDataCenter` | 数据存储 + typed 事件通知 |
| **UI** | `UIWidget` | 响应 DataCenter 事件自动刷新显示 |

---

## 快速开始

### 1. GF 入口

场景中挂载 `GF`，它是全局入口，自动扫描场景中的模块并启动首个 Flow。

```csharp
// 在场景中创建空 GameObject → 挂载 GF
// GF.Awake() 时自动初始化 DataManager、FlowManager
// GF.Start() 时自动 AutoRegisterSceneModules 并启动流程
```

### 2. 创建一个模块

```csharp
public class MyModule : MonoBehaviour, IModule
{
    public Type InterfaceType => typeof(IMyModule);
    public int Priority => 5;

    public void OnStart()
    {
        // 模块初始化，自动被 GF 调用
    }
}
```

`IModule` 提供默认空实现的完整生命周期：`OnAwake` / `OnStart` / `OnUpdate` / `OnLateUpdate` / `OnDestroy` / `OnApplicationQuit`，按需重写即可。

### 3. 创建一个数据中心

```csharp
public class MyDataCenter : IDataCenter
{
    private Dictionary<int, PlayerData> _players = new();
    private int _nextId = 1;

    public event Action<int, string> OnNameChanged;

    public int AddPlayer(PlayerData data) { ... }
    public PlayerData GetPlayer(int id) { ... }
}
```

在 `DataManager` 中注册：

```csharp
// DataManager.cs
private static MyDataCenter _myData;
public static MyDataCenter MyData => _myData ??= new MyDataCenter();
```

访问：`DataManager.MyData.AddPlayer(...)`

### 4. 创建一个 UI

```csharp
public class PlayerNameLabel : UIWidget
{
    [SerializeField] private TextMeshProUGUI _text;

    protected override void OnBind()
    {
        DataManager.MyData.OnNameChanged += (id, name) => _text.text = name;
    }
    protected override void OnUnbind()
    {
        DataManager.MyData.OnNameChanged -= (id, name) => _text.text = name;
    }
}
```

### 5. 创建一个 Flow

```csharp
public class MainFlow : FlowBase
{
    public void Enter()
    {
        GF.GetModule<MyModule>();
    }
    public void Exit() { }
}
```

在 `GF.Start()` 中通过 `FlowManager.StartFlow<MainFlow>()` 启动。

---

## 文件结构

```
Assets/FModa/
├── Architecture/
│   ├── Global/
│   │   ├── GF.cs                   全局入口，模块注册/生命周期派发
│   │   ├── DataManager.cs          统一数据中心管理器（懒加载）
│   │   ├── GenericTable.cs         泛型数据表（冷数据/配置）
│   │   └── IDataCenter.cs          数据中心接口 + 创建文档
│   ├── UI/
│   │   └── UIWidget.cs             UI 组件基类
│   └── MVC/                        （旧架构存档，已不再维护）
├── Modules/
│   ├── IModule.cs                  模块生命周期接口
│   ├── Network/                    TCP 网络框架
│   └── Hotfix/                     XLua 热更新管线
└── Debug/
    └── GameLogger.cs               游戏内日志面板

Assets/Example/                     使用示例
├── Scripts/
│   ├── Flows/                      ExampleFlow
│   ├── Modules/RobotAttack/        战斗模块 + CombatDataCenter
│   ├── Renders/Robot/              机器人渲染
│   └── UI/                         UnitHpPanel（UIWidget 示例）
```

---

## 设计原则

### 数据 → DataCenter，不放在 Model 里

所有运行时数据统一由 `IDataCenter` 的实现类管理，数据变更通过 **typed 事件**通知 UI 和模块。每个数据域一个子类，独立事件、零类型过滤开销。

### UI → UIWidget，不经过 Controller

UI 直接从对应 DataCenter 订阅事件、刷新显示，不需要 Controller 层。U IWidget 基类统一管理订阅/取消订阅生命周期。

### 冷热数据分离

| 场景 | 方案 |
|------|------|
| 高频变更（战斗、背包） | 实现 `IDataCenter` 建 typed 子类 |
| 冷数据/配置表 | `GenericTable` 泛型 API |
| 不确定 | 先用 `GenericTable`，后续可迁移 |

### 模块即编排者

模块不做具体实现（不写伤害公式、不操作 UI），只做三件事：**初始化 → 订阅事件 → 委托给 System**。

---

## 依赖

- Unity 2022.3+
- XLua（热更新管线）
- Newtonsoft.Json（网络消息序列化）
- UniTask（热更新异步）
- TextMeshPro（UI 示例）
