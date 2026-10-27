---
name: fmoda-module-design
description: 在 FModa/ECMVC Unity 项目中新增或修改一个模块时使用。涵盖数据结构、数据中心、模块接口、Manager、DataManager 注册、Flow 接入的完整六步流程，标准目录与命名空间、可直接套用的代码骨架，以及注册重复、生命周期不触发、事件退订失败等必须避开的陷阱。适用于 Input/Network/Modding 这类内置模块，也适用于 Unit 这类业务模块。
---

# 模块设计规范

模块是本项目的基本业务单元。一个模块必须满足：

- 内部高内聚，**整体复制到另一个项目不会因为引用本项目的其他模块而报错**；
- 对外只通过接口暴露能力；
- 只有模块可以直接访问数据中心；
- 生命周期由 `IModule` 定义，由 GF 派发，不依赖 Unity 消息顺序。

---

## 0. 先判断：真的需要新模块吗？

| 你的需求 | 是否需要新模块 |
|---|---|
| 有运行时状态，并且有规则决定状态怎么变 | **是**，建模块 + 数据中心 |
| 只是静态配置、数值表、只读资源 | 否，用 `ScriptableObject` / `GameConfig` |
| 纯算法、格式化、无状态工具 | 否，普通静态类 |
| 只是一段特效、动画、跟随 | 否，写 Render |
| 只是面板显示和按钮 | 否，写 UI，调用已有模块 |
| 需要协调两个已有模块的先后顺序 | 否，写在 Flow 里 |
| 需要被多个流程复用、有独立生命周期 | **是** |

模块的最小判据：**它拥有或管理一份需要跨场景/跨视图共享的状态，并且这份状态的变化需要被广播。** 两者都不满足，就不要造模块。

---

## 1. 标准目录与命名

以 `Inventory` 模块为例，放在 `Assets/FModa/Modules/Inventory/`：

```
Assets/FModa/Modules/Inventory/
├── InventoryManager.cs          ← 模块管理器（实现接口 + IModule）
├── Interface/
│   └── IInventoryManager.cs     ← 对外契约
├── Data/
│   ├── InventoryData.cs         ← 纯数据（IData）
│   └── InventoryDataCenter.cs   ← 数据中心（DataCenter<T>）
├── Doc/
│   └── README.md                ← 用法、API、注意事项
└── Example/                     ← 可选：示例代码/测试
```

命名约定：

| 角色 | 命名 | 示例 |
|---|---|---|
| 模块管理器 | `<Domain>Manager` | `UnitManager`、`NetworkManager` |
| 模块接口 | `I<Domain>Manager` | `IUnitManager`、`IInputManager` |
| 数据结构 | `<Domain>Data` | `UnitData`、`NetData` |
| 数据中心 | `<Domain>DataCenter` | `UnitDataCenter` |
| 命名空间 | `FModa.<Domain>` | `FModa.InputSystem`、`FModa.Net`、`FModa.Modding` |
| 事件 | `On<SomethingChanged>` | `OnHpChanged`、`OnFlowEnd` |

规则：

- **一个模块一个命名空间**，所有类都放进去。示例里的 `Conquer.Unit` 是游戏侧命名空间，可以照这个形式换成自己的项目名。
- 不要往全局命名空间里新增类型。`GF`、`IModule`、`IFlow`、`UIBase`、`UIManager`、`DataCenter` 这些是历史遗留的全局类型，新代码应放进明确命名空间。
- 框架层 `Assets/FModa/Architecture/` 只放可复用的框架代码；业务模块放 `Assets/FModa/Modules/` 或游戏自己的模块目录。

---

## 2. 六步实现流程

### 第 1 步：定义数据结构（`IData`）

```csharp
using System;
using System.Collections.Generic;

namespace FModa.Inventory
{
    [Serializable]
    public class InventoryData : IData
    {
        public long ownerId;
        public int capacity = 20;
        public List<ItemStack> items = new();
    }

    [Serializable]
    public class ItemStack
    {
        public string itemId;
        public int count;
    }
}
```

要求：

- 只放字段/简单属性，**不要放 MonoBehaviour、不要查表、不要发事件、不要写业务判断**。
- 需要 Inspector 序列化或网络传输时加 `[Serializable]`。
- 实现 `IData` 标记接口。
- 数据里可以放集合，但集合的增删改由数据中心/模块负责。

### 第 2 步：定义数据中心（`DataCenter<T>`）

```csharp
using System;
using System.Collections.Generic;
using FModa.Data;

namespace FModa.Inventory
{
    public class InventoryDataCenter : DataCenter<InventoryData>, IDataCenter
    {
        /// <summary>容量变化时触发：(ownerId, newCapacity)</summary>
        public event Action<long, int> OnCapacityChanged;

        /// <summary>语义方法：只改一个字段，并广播业务事件</summary>
        public bool SetCapacity(long ownerId, int capacity)
        {
            if (!datas.TryGetValue(ownerId, out var data)) return false;
            if (capacity < 0) return false;

            data.capacity = capacity;
            OnCapacityChanged?.Invoke(ownerId, capacity);
            return true;
        }

        public bool TryAddItem(long ownerId, string itemId, int count)
        {
            if (!datas.TryGetValue(ownerId, out var data)) return false;
            if (count <= 0) return false;

            var stack = data.items.Find(s => s.itemId == itemId);
            if (stack == null)
            {
                stack = new ItemStack { itemId = itemId };
                data.items.Add(stack);
            }
            stack.count += count;
            return true;
        }
    }
}
```

要求：

- 继承 `DataCenter<T>`，同时实现标记接口 `IDataCenter`。
- **不要重写一套字典**；基类已提供 `datas`、`id`、`AddData / GetData / UpdateData / RemoveData / Clear / HasId / HasData / GetId`。
- 子类只补语义方法（`SetCapacity`、`TryAddItem`），并负责在需要时广播 **typed 事件**。
- `GetData` 返回的是**引用**，直接改字段不会触发任何事件。要通知，就在语义方法里改完再 `Invoke`。
- 当前基类**没有** `OnDataAdded / OnDataRemoved / OnCleared` 事件（README 中提到的这三个事件在代码里不存在）。如果业务确实需要，在自己的 DataCenter 里显式定义并在 `AddData / RemoveData / Clear` 之后广播。
- 数据中心只广播事实，不替视图做处置；视图该刷新还是该关闭，由模块决定。

### 第 3 步：定义模块接口（对外契约）

```csharp
using System;

namespace FModa.Inventory
{
    public interface IInventoryManager
    {
        /// <summary>容量变化时触发：(ownerId, newCapacity)</summary>
        event Action<long, int> OnCapacityChanged;

        /// <summary>模块请求结束当前流程时触发</summary>
        event Action OnClosed;

        /// <summary>由 Flow 在进入时调用</summary>
        void Init();

        int GetCapacity(long ownerId);
        bool SetCapacity(long ownerId, int capacity);
        bool TryAddItem(long ownerId, string itemId, int count);
        int GetItemCount(long ownerId, string itemId);

        /// <summary>请求结束当前流程</summary>
        void EndFlow();
    }
}
```

要求：

- 只暴露**视图和其他系统真正需要**的方法与事件。
- 事件用 `event Action` / `event Action<T>`，命名 `OnXxx`。
- **不要把内部可变字典直接返回出去**。现有 `UnitManager.GetAllUnitData()` 返回了内部字典，这是示例里的便利写法；新模块应返回拷贝、只读视图或逐项查询，否则调用方可以绕过事件改数据。
- 不要在业务接口里重复声明 `InterfaceType` / `Priority`。它们是 `IModule` 的关注点；现有 `IUnitManager` 里的这两行是接口生成工具留下的冗余，不要继续扩散。
- 业务接口**不继承** `IModule`。外部获取模块时用具体类型：`GF.GetModule<InventoryManager>()`。

### 第 4 步：实现模块管理器

有两种形态，按是否需要在 Inspector 里拖引用选择：

**A. 纯逻辑模块（默认推荐）**：普通类，不需要挂场景。

```csharp
using System;
using FModa.Data;

namespace FModa.Inventory
{
    public class InventoryManager : IInventoryManager, IModule
    {
        public Type InterfaceType => typeof(IInventoryManager);
        public int Priority => 5; // 数字越小越先执行

        // 每次用属性取，绝不缓存到字段：DataManager.PreWarm 会替换实例
        private InventoryDataCenter Data => DataManager.Inventory;

        public event Action<long, int> OnCapacityChanged;
        public event Action OnClosed;

        public void Init()
        {
            // 由 Flow 在进入时调用：加载配置、生成初始数据等
        }

        public void EndFlow()
        {
            OnClosed?.Invoke();
        }

        public void OnAwake()
        {
            // 订阅数据中心的事件放在 OnAwake，不放在 Unity 的 Awake
            Data.OnCapacityChanged += HandleCapacityChanged;
        }

        public void OnDestroy()
        {
            Data.OnCapacityChanged -= HandleCapacityChanged;
            OnCapacityChanged = null;
            OnClosed = null;
        }

        private void HandleCapacityChanged(long ownerId, int capacity)
        {
            // 数据中心事件 → 模块业务事件，视图只订阅模块
            OnCapacityChanged?.Invoke(ownerId, capacity);
        }

        public int GetCapacity(long ownerId)
        {
            var data = Data.GetData(ownerId);
            return data?.capacity ?? 0;
        }

        public bool SetCapacity(long ownerId, int capacity)
            => Data.SetCapacity(ownerId, capacity);

        public bool TryAddItem(long ownerId, string itemId, int count)
            => Data.TryAddItem(ownerId, itemId, count);

        public int GetItemCount(long ownerId, string itemId)
        {
            var data = Data.GetData(ownerId);
            if (data == null) return 0;
            var stack = data.items.Find(s => s.itemId == itemId);
            return stack?.count ?? 0;
        }
    }
}
```

**B. 场景模块**：需要在 Inspector 拖预制体、Transform、配置时，继承 `MonoBehaviour`。

```csharp
public class UnitManager : MonoBehaviour, IModule, IUnitManager
{
    public Type InterfaceType => typeof(IUnitManager);
    public int Priority => 5;

    public GameObject unitPrefab; // Inspector 引用

    public void OnAwake() { /* 订阅 */ }
    public void OnDestroy() { /* 退订，必须幂等 */ }
    // ...
}
```

Manager 的硬性规则：

1. **生命周期方法必须 `public`**。C# 私有成员不参与接口映射；`void OnDestroy()` 默认 private，不会实现 `IModule.OnDestroy`，而接口有默认空实现，编译器不会报错，方法永远不会被 GF 调用。
2. **订阅放在 `OnAwake`**。GF 与场景对象的 Unity `Awake` 顺序未定义；`OnAwake` 由 GF 主动调用，时序确定。
3. **用命名方法订阅，不要用 lambda**。lambda 每次求值都是新委托，退订时 `-=` 匹配不上。
4. **模块只访问自己的数据中心**。通过 `DataManager.X` 属性获取，不要缓存实例。
5. **模块逻辑走 `OnUpdate` / `OnLateUpdate`**，默认 0.1s 逻辑帧；不要用 Unity 的 `Update()` 做业务逻辑。Render 才允许每帧。
6. **模块不直接引用其他模块的具体类**。跨模块协作交给 Flow；确实需要依赖时，依赖接口并由 Flow 注入/编排。
7. **对外只暴露业务事件**。DataCenter 的事件在模块里转发，视图不要直接订阅 DataCenter。
8. **可移植性**：模块内不要 `using` 其他模块的命名空间，不要引用 `Assets/Example`。
9. **纯逻辑模块必须有无参构造**：`GF.CreateModule<T>()` / `GF.GetModule<T>()` 的自动创建路径依赖 `new()` 或 `Type.GetConstructor(Type.EmptyTypes)`。需要外部依赖时，通过 Flow 调用 `Init(...)` 注入，不要在构造函数里访问 GF。

> ⚠️ **MonoBehaviour 模块的 `OnDestroy` 可能被调用两次**：`GF.RemoveModule<T>()` 会显式调一次 `IModule.OnDestroy()`，随后销毁 GameObject 时 Unity 还会发一次 `OnDestroy` 消息；退出播放时 `DispatchModuleDestroy()` 与 Unity 消息也可能重叠。清理逻辑必须**幂等**：
>
> ```csharp
> private bool _disposed;
> public void OnDestroy()
> {
>     if (_disposed) return;
>     _disposed = true;
>     Data.OnCapacityChanged -= HandleCapacityChanged;
>     OnCapacityChanged = null;
> }
> ```
>
> 如果不需要 Unity 消息，也可以显式实现 `void IModule.OnDestroy()` 并另写 private `void OnDestroy()`，但两处仍应共用同一个幂等清理方法。

### 第 5 步：在 `DataManager` 注册数据中心

`Assets/FModa/Architecture/Data/DataManager.cs`：

```csharp
private static InventoryDataCenter _inventory;
public static InventoryDataCenter Inventory => _inventory ??= new InventoryDataCenter();
```

- 懒加载属性是唯一入口，所有模块通过 `DataManager.Inventory` 访问。
- 需要在启动时就准备好的数据中心，在 `PreWarm()` 里 `new` 一次。
- **模块里不要缓存 `DataManager.X` 到字段**：`PreWarm()` 会整体替换实例，旧引用会变成孤儿。
- 新增数据中心必须在这里注册，否则别处无法正常获取。

### 第 6 步：在 Flow 中启动/销毁模块

```csharp
using FModa.Inventory;

public class InventoryFlow : IFlow
{
    private IInventoryManager inventory;

    public void Enter()
    {
        // 1. 获取（不存在会自动创建并注册）
        inventory = GF.GetModule<InventoryManager>();

        // 2. 初始化
        inventory.Init();

        // 3. 用命名方法订阅流程结束
        inventory.OnClosed += HandleClosed;
    }

    public void Exit()
    {
        inventory.OnClosed -= HandleClosed;
        GF.RemoveModule<InventoryManager>();
    }

    private void HandleClosed()
    {
        GF.flow.SwitchFlow<NextFlow>(this);
    }
}
```

规则：

- Flow 是**唯一**负责启动/销毁模块、决定模块顺序的地方。Render/UI 不创建模块。
- `SwitchFlow<T>(this)` 会先调用当前流程的 `Exit()`，再 `Enter()` 新流程。
- Flow 里订阅模块事件也要用命名方法，并在 `Exit()` 退订。示例 `ExampleFlow.cs` 用了 lambda 且未退订，不要照抄。
- 一个流程负责一组模块的编排；模块之间的数据交换通过各自的接口在 Flow 里完成。
- 全局同时只有一个 `GF.flow.CurrentFlow`，不要绕过 `FlowManager` 自己保存流程。

---

## 3. 注册路径对照表

| 方式 | 存储 key | 生命周期 | 适用场景 |
|---|---|---|---|
| 场景挂载 + `AutoRegisterSceneModules()` | `module.InterfaceType` | 自动 `OnAwake` + `OnStart` | 需要 Inspector 引用的场景模块 |
| `GF.CreateModule<T>()` | `module.InterfaceType` | 自动创建实例并触发 | 代码创建模块 |
| `GF.RegisterModule<T>(instance)` | `typeof(T)` | 触发 | 非场景实例；注意 key 与上面不同 |
| `GF.GetModule<T>()` | 通过「具体类型 → 接口」反查 | 不存在时自动创建 | 获取模块的唯一写法 |
| `GF.RemoveModule<T>()` | 通过反查 | 触发 `OnDestroy`，销毁 GameObject | Flow 退出时清理 |
| `GF.ClearAllModules()` | 全部 | 逆序触发 `OnDestroy` | 切场景 |

必须记住的三条：

1. **场景模块让它自动注册，不要再手写 `RegisterModule`**。`RegisterModule<T>` 用 `typeof(T)`，自动注册用 `InterfaceType`，两者各注册一次会得到两条记录，生命周期跑两遍。
2. **获取用具体类型**：`GF.GetModule<UnitManager>()`。`GetModule<T>` 的约束是 `where T : class, IModule`，业务接口不继承 `IModule`，`GF.GetModule<IUnitManager>()` 编译不过。
3. **`Priority` 只在同一个逻辑帧内决定顺序**，数字越小越先执行。现有模块多用 5，`ModdingManager` 用 10；新模块选值时在 Doc 里写清楚理由，不要随手填。

---

## 4. 文档模板（`Doc/README.md`）

每个模块必须有介绍文档，至少包含：

```markdown
# <Domain> 模块

## 用途
一句话说明这个模块解决什么问题。

## 对外接口
- `I<Domain>Manager`：列出主要方法和事件
- 事件触发时机

## 数据结构
- `<Domain>Data` 的字段含义
- `<Domain>DataCenter` 的语义方法

## 生命周期
- 谁创建、谁初始化、谁销毁
- 依赖哪些模块/数据中心

## 使用示例
最小可运行代码。

## 注意事项
事件退订、线程、场景切换、初始化顺序等。
```

文档里的成员名必须与代码一致。发现漂移时，在同一次改动里修正。

---

## 5. 自检清单

完成模块后逐条确认：

- [ ] 目录结构符合 `<Domain>Manager.cs` / `Interface/` / `Data/` / `Doc/`
- [ ] 所有类在同一个命名空间，没有新增全局命名空间类型
- [ ] 数据类只含数据，实现 `IData`
- [ ] DataCenter 继承 `DataCenter<T>`，没有重复实现字典
- [ ] 需要通知的变化都有 typed 事件，事件命名 `OnXxx`
- [ ] 接口只暴露视图/其他系统真正需要的能力
- [ ] Manager 实现 `IModule`，`InterfaceType` 指向正确的业务接口
- [ ] 生命周期方法都是 `public`，清理逻辑幂等
- [ ] 订阅在 `OnAwake`，用命名方法，`OnDestroy` 成对退订
- [ ] 模块内只通过 `DataManager.X` 属性访问数据中心，没有缓存
- [ ] 模块不引用其他模块的具体类
- [ ] DataManager 中已注册懒加载属性
- [ ] Flow 中正确创建/初始化/移除模块，事件成对退订
- [ ] 场景模块没有重复 `RegisterModule`
- [ ] `Doc/README.md` 已写，成员名与代码一致
- [ ] Unity 编译通过，Play 后模块只注册一次、生命周期日志正常
- [ ] 把模块单独复制到空项目（连同框架依赖）不会引用到其他业务模块

---

## 6. 反模式速查

| 反模式 | 后果 | 正确做法 |
|---|---|---|
| 在 Render/UI 里 `new XxxManager()` | 模块不受 GF 管理，生命周期全失效 | `GF.GetModule<XxxManager>()` |
| `GF.GetModule<IInventoryManager>()` | 泛型约束不满足，编译失败 | 用具体类型 |
| 场景模块 + 手写 `RegisterModule` | 同一模块两条记录，`OnAwake` 跑两遍 | 只保留自动注册 |
| private `OnDestroy()` | `IModule.OnDestroy` 不被调用 | 写成 public，并做幂等 |
| 在 Unity `Awake` 里订阅数据中心 | 时序未定义，可能订阅到未初始化的模块 | 移到 `OnAwake` |
| lambda 订阅 | 无法退订，模块销毁后仍被回调 | 命名方法 + 成对 `-=` |
| 缓存 `DataManager.X` | `PreWarm` 替换实例后引用失效 | 每次用属性取 |
| 直接返回内部 `Dictionary` | 调用方绕过事件改数据 | 返回拷贝/只读视图/查询方法 |
| 模块里直接 `GF.GetModule<OtherManager>()` | 模块耦合，无法单独搬运 | 交给 Flow 编排 |
| 用 Unity `Update()` 跑模块逻辑 | 绕开逻辑帧，顺序不可控 | `OnUpdate` |
| 在 `Assets/FModa/Architecture/` 写业务 | 框架层被业务污染 | 放 `Assets/FModa/Modules/` |
| 数据中心里写业务规则 | 数据和逻辑耦合 | 语义只做校验/存储，规则放模块 |
