---
name: fmoda-module-design
description: 在 FModa/ECMVC Unity 项目中新增或修改模块、Bond 或 Flow 时使用。涵盖数据结构、数据中心、模块接口、Manager、DataManager 注册、Bond 粘合与 Flow 步骤编排的完整流程，以及注册重复、生命周期不触发、事件退订失败、Flow 上帝类和 PM 业务化等必须避开的陷阱。
---

# 模块、Bond 与 Flow 设计规范

模块是本项目的基本业务单元。一个模块必须满足：

- 内部高内聚，**整体复制到另一个项目不会因为引用本项目的其他模块而报错**；
- 对外只通过接口暴露能力；
- 只有模块可以直接访问数据中心；
- 生命周期由 `IModule` 定义，由 GF 派发，不依赖 Unity 消息顺序。

Bond 和 Flow 不是模块的替代品：Flow 负责步骤编排、单个模块的创建、初始化、调用和清理，并在自己内部实现流程切换；Bond 负责模块与其他模块、Render、UI 的可复用粘合。

---

## 0. 先判断：代码到底应该放哪里？

**第一步永远是查内置模块**：`Assets/FModa/Modules/` 下已有 Input、Network、Hotfix、Modding、Multilingual 等可直接使用的模块，清单见 `Skills/README.md`。需求命中就调用或扩展现有模块，不要另起一套。

| 你的需求 | 归属 |
|---|---|
| 有运行时状态，并且有规则决定状态怎么变 | **模块 + 数据中心**（先确认没有同类内置模块） |
| 只是静态配置、数值表、只读资源、常量 | **只放 `Global/GameConfig.cs`**，通过 `GF.config` 读取；每个字段注释用途 |
| 与模块无关的纯算法、纯格式化、无状态计算 | **只放 `Global/PM.cs`**；不得访问 Module、DataCenter、Bond、Flow、Render、UI |
| 会被两个及以上模块/视图使用的枚举 | **只放 `Global/GameEnum.cs`**（`FModa.Enum`） |
| 只是一段特效、动画、跟随 | Render；场景对象 / 预制体走 `GF.sgo` |
| 只是面板显示和按钮 | UI：继承 `UIBase`，预制体放 `Resources/UI`，用 `GF.ui.OpenUI` |
| 创建、获取、初始化、调用或清理单个模块 | **Flow** 或模块的实际使用者；不涉及其他模块和视图时无需 Bond |
| 协调两个模块、转换模块数据、组合模块与 Render / UI | **Bond**，供所有流程复用 |
| 按顺序执行单模块操作和几个已经封装好的 Bond 步骤 | **Flow**；不在 Flow 内实现多模块或跨视图粘合的细节 |
| 切换流程 | **只能在 Flow 中实现**；其他层通过事件或返回值提出请求 |
| 需要拥有独立状态和业务规则，并被多个流程复用 | **模块**；如果只是复用跨层动作，则用 Bond，不要新造模块 |

模块的最小判据：**它拥有或管理一份需要跨场景/跨视图共享的状态，并且这份状态的变化需要被广播。** 两者都不满足，就不要造模块。

> 静态数据、纯工具、公用枚举分别上移到 `GameConfig` / `PM` / `GameEnum`。跨模块或跨层复用不是 PM 的判据，而是 Bond 的典型判据。

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
- **静态数据只加在 `Global/GameConfig.cs`**：字段要有注释 / `[Tooltip]` 说明用途，运行时用 `GF.config.xxx` 读取。不要为某模块单独新建配置类或常量类。
- **纯静态工具方法只加在 `Global/PM.cs`**（命名空间 `FModa`）：仅接受与模块无关的计算、格式化和无状态算法；涉及 Module、DataCenter、Bond、Flow、Render、UI 的方法不得放入，跨层公共动作交给 Bond。
- **公用枚举只加在 `Global/GameEnum.cs`**（命名空间 `FModa.Enum`）：跨模块使用的枚举统一放这里，不要在各自 Data 里重复定义再写转换适配器。
- **不要重复造内置模块**：动手前先看 `Assets/FModa/Modules/` 和 `Skills/README.md` 的清单；已有能力优先扩展，而不是新建一个同职责模块。

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
        // 保持公共无参构造；Code 由 DataManager 在自动注册时分配。

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
- 当前基类**没有** `OnDataAdded / OnDataRemoved / OnCleared` 事件（旧版文档提到的这三个事件在代码里不存在）。如果业务确实需要，在自己的 DataCenter 里显式定义并在 `AddData / RemoveData / Clear` 之后广播。
- 数据中心只广播事实，不替视图做处置；视图该刷新还是该关闭，由模块决定。

#### id 约定

从 2026-11 起，`DataCenter<T>` 的存储 key 不再是一个裸自增数字，而是「数据中心 Code + 本地 id」合成的 `long`，位布局见 `Assets/FModa/Architecture/Data/DataCenterId.cs`：

- Code 占高 12 位（0 ~ 4095），本地 id 占低 51 位；符号位恒为 0，`-1` 永远不是合法 id。
- 数据中心保持公共无参构造，`DataManager.Get<T>()` 首次获取时自动调用 `Init` 分配非零 Code。
- Code 只保证当前运行时内的中心隔离；注册顺序改变后允许不同，不作为存档或网络中的稳定类型标识。
- 模块对外只传合成 id，不传本地 id；需要展示时用 `ToLocalId(id)`。运行时 id 不是跨存档稳定标识，导入后会重新分配。
- 查询用 `TryGetData(id, out data)`；`GetData` 收到别的中心的 id 会打警告并返回 `null`。
- `AddData` 只接受 `Id == -1` 的新数据；`RemoveData / Clear` 会把数据 Id 重置为 `-1`；`UpdateData` 不接受属于别的 Id 的数据。这层保护由基类完成，子类不要绕过它去直接改 `datas` 的 key。

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

        /// <summary>由 Bond 在进入时调用</summary>
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
            // 由 Bond 在进入时调用：加载配置、生成初始数据等
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

        public void OnRemove()
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

**B. 不允许场景模块**：Manager 只能是普通 C# 类，不得继承 `MonoBehaviour`，也不得挂载到场景或由 GF 扫描注册。预制体、Transform 和其他场景对象由 Render 与 `GF.sgo` 管理；静态配置放 `GameConfig`；模块需要的外部参数通过 `Init(...)` 等显式方法注入。

Manager 的硬性规则：

1. **生命周期方法必须 `public`，销毁钩子是 `OnRemove()`**。`IModule` 的方法是 `OnAwake / OnStart / OnUpdate / OnLateUpdate / OnRemove / OnApplicationQuit`，不是 `OnDestroy`。写成 `void OnDestroy()`（或 private）不会实现 `IModule.OnRemove`，接口又有空默认实现，编译器不报错，方法永远不会被 GF 调用。
2. **订阅放在 `OnAwake`**。`OnAwake` 由 GF 在显式注册时主动调用，不依赖 Unity 消息顺序。
3. **用命名方法订阅，不要用 lambda**。lambda 每次求值都是新委托，退订时 `-=` 匹配不上。
4. **模块只访问自己的数据中心**。通过 `DataManager.X` 属性获取，不要缓存实例。
5. **模块逻辑走 `OnUpdate` / `OnLateUpdate`**，默认 0.1s 逻辑帧；不要使用 Unity 的 `Update()` 做业务逻辑。Render 才允许每帧。
6. **模块不直接引用其他模块的具体类**。跨模块协作交给 Bond；确实需要依赖时，依赖接口并由 Bond 注入或组合。
7. **对外只暴露业务事件**。DataCenter 的事件在模块里转发，视图不要直接订阅 DataCenter。
8. **可移植性**：模块内不要 `using` 其他模块的命名空间，不要引用 `Assets/Example`。
9. **所有自动创建的模块必须有无参构造**：`GF.CreateModule<T>()` / `GF.GetModule<T>()` 的自动创建路径依赖 `new()` 或 `Type.GetConstructor(Type.EmptyTypes)`。需要外部依赖时，通过 `Init(...)` 注入，不要在构造函数里访问 GF。
10. **日志走 `GF.Log / GF.LogWarning / GF.LogError`**：它们受 `GF.debugMode` 控制，发布版可以整体关闭；不要直接 `Debug.Log`。

模块只实现 `OnRemove()` 清理即可，没有 Unity `OnDestroy()` 等第二套生命周期入口。GF 的 `CreateModule / GetModule / RegisterModule` 会拒绝 MonoBehaviour 模块。

### 第 5 步：通过 `DataManager` 自动注册数据中心

`Assets/FModa/Architecture/Data/DataManager.cs`：

```csharp
public static InventoryDataCenter Inventory => Get<InventoryDataCenter>();
```

也可以不使用强类型属性，直接调用 `DataManager.Get<InventoryDataCenter>()`。首次获取会自动执行 `Register<T>()`、调用 `Init` 并分配当前运行时的 CenterCode；同类型重复获取返回同一实例。

- 数据中心必须有公共无参构造；不再维护 `DataCenterCodes`、手写字段或 `CreateRegistry()`。
- `PreWarm()` 会重置所有已注册中心并保留当前 Code；未注册中心仍在首次 `Get<T>()` 时创建。
- **模块里不要缓存 `DataManager.X` 或 `Get<T>()` 的结果**：`PreWarm()` 和存档导入会替换实例。
- 子类若维护额外索引，应重写 `RebuildAfterRestore`；需要拒绝导入时重写 `ValidateImportedData`；特殊序列化字段使用 `SerializeData / DeserializeData`。
- 存档导出支持当前 `CenterCodes` 和 `ItemIdsByCenter`。磁盘不保存条目 id 或计数器，`Replace / Append` 都重新发号，没有 `IdMappings`。
- 导入通过 `centerType` 匹配当前已注册类型；目标类型未注册时导入失败，不会根据文件字符串任意创建类型。

### 第 6 步：Flow 管理单模块，Bond 封装多模块与跨层粘合

只涉及一个模块时，Flow 可以直接获取、初始化、调用和移除它，无需为了单模块操作额外制造 Bond。若动作同时组合第二个模块或 Render / UI，则必须将组合步骤放入 Bond：

```csharp
using System.Collections.Generic;
using FModa.Inventory;

public class InventoryFlow : IFlow
{
    private IInventoryManager inventory;

    // 签名固定：lastFlow 是上一流程（首次启动为 null），list 是上一流程传来的数据
    public void Enter(IFlow lastFlow, List<object> list = null)
    {
        inventory = GF.GetModule<InventoryManager>();
        inventory.Init();
        inventory.OnClosed += HandleClosed;
    }

    public void Update()
    {
        // IFlow 没有默认实现；无逻辑帧步骤就留空
    }

    public void Exit()
    {
        if (inventory == null) return;
        inventory.OnClosed -= HandleClosed;
        GF.RemoveModule<InventoryManager>();
        inventory = null;
    }

    // 唯一允许的自定义 Flow 方法：流程切换处理
    private void HandleClosed()
    {
        GF.flow.SwitchFlow<NextFlow>(this);
    }
}
```

规则：

- **Flow 可以管理单个模块**：允许 `GF.GetModule / CreateModule / RemoveModule`，也允许直接调用该模块的初始化和业务 API。
- **Bond 负责粘合**：可访问模块、Render、UI，封装多模块协作、模块间转换、实体生成并联动视图、id 注入和 UI 刷新等组合步骤。
- **Flow 不直接操作视图细节**：一旦模块操作需要与 Render / UI 组合，就将这部分放进 Bond，由 Flow 按顺序调用。
- `IFlow` 的 `Enter / Update / Exit` 是固定契约，必须全部实现；它们内部可以排列单模块操作和 Bond 步骤，自定义方法仍只允许用于流程切换。
- `GF.GetBond<T>()` 在不存在时自动创建，同一具体类型重复获取会复用；`GF.CreateBond<T>()` 可显式创建。需要外部依赖的 Bond 仍应提供无参构造。
- Bond 被多个流程复用时，事件订阅、模块初始化和清理必须成对且幂等，避免上一个流程残留订阅。
- **流程切换是死规则**：只有 Flow 能调用 `GF.flow.SwitchFlow / SwitchSceneAndFlow / SwitchSceneAndFlowAsync`。Bond、Module、Render、UI 只能发事件或返回结果。
- `SwitchFlow<T>(this, list)` 会先调用当前流程的 `Exit()`，再以 `Enter(this, list)` 进入新流程；跨场景切换会等待场景加载后延迟执行。
- Flow 订阅 Module 或 Bond 事件也要用命名方法，并在 `Exit()` 退订。
- 全局同时只有一个 `GF.flow.CurrentFlow`，不要绕过 `FlowManager` 自己保存流程。

---

## 3. 注册路径对照表

| 方式 | 存储 key | 生命周期 | 适用场景 |
|---|---|---|---|
| `GF.CreateModule<T>()` | `module.InterfaceType` | 自动创建普通 C# 实例并触发 | Flow 的单模块管理、Bond 的跨层步骤或其他实际使用者显式创建 |
| `GF.RegisterModule<T>(instance)` | `typeof(T)` | 触发 | 注册已有普通 C# 实例；注意 key 与 Create / Get 路径不同 |
| `GF.GetModule<T>()` | 通过「具体类型 → 接口」反查 | 不存在时自动创建普通 C# 实例 | Flow / Module / Bond / Render / UI 按职责获取 |
| `GF.RemoveModule<T>()` | 通过反查 | 触发 `IModule.OnRemove()` | Flow 的单模块清理、Bond 封装的组合清理或全局服务清理 |
| `GF.ClearAllModules()` | 全部 | 逆序触发 `IModule.OnRemove()` | 框架级清理 |
| 退出播放 / GF 销毁 | 全部 | `DispatchModuleDestroy()` 逆序触发 `IModule.OnRemove()` | 自动 |

必须记住：

1. **所有模块都是普通 C# 类，不得继承 `MonoBehaviour` 或挂载到场景**；`CreateModule / GetModule / RegisterModule` 会拒绝 MonoBehaviour 模块，GF 不再提供场景自动注册。
2. **获取用具体类型**：`GF.GetModule<UnitManager>()`；业务接口不继承 `IModule`，不能作为泛型参数。
3. **`Priority` 只在同一个逻辑帧内决定顺序**，数字越小越先执行。
4. **模块 API 的位置不等于调用权限**：GF 提供底层能力，Flow 可以管理单个模块；多模块协作和模块与 Render / UI 的组合必须由 Bond 封装。

---

## 4. 文档模板（`Doc/README.md`）

每个模块必须有介绍文档，至少包含：

```markdown
# <Domain> 模块

## 用途
一句话说明这个模块解决什么问题。

## 对外接口
- `I<Domain>Manager`：主要方法和事件
- 事件触发时机

## 数据结构
- `<Domain>Data` 的字段含义
- `<Domain>DataCenter` 的语义方法

## 生命周期
- 由 GF 显式创建 / 注册、Flow 的单模块管理，还是由哪个 Bond 步骤执行跨层准备和清理
- 依赖哪个数据中心；不得依赖其他业务模块

## Bond / Flow 接入
- 哪些单模块操作由 Flow 直接处理，哪些多模块或跨层动作由哪个 Bond 封装
- Flow 只排列单模块操作和 Bond 步骤，不复制模块内部规则或跨层粘合细节

## 使用示例
最小可运行代码。

## 注意事项
事件退订、线程、场景切换、初始化顺序等。
```

文档里的成员名必须与代码一致。发现漂移时，在同一次改动里修正。

---

## 5. 自检清单

完成模块、Bond 或 Flow 后逐条确认：

- [ ] 目录结构符合 `<Domain>Manager.cs` / `Interface/` / `Data/` / `Doc/`
- [ ] 所有类在同一个命名空间，没有新增全局命名空间类型
- [ ] 数据类只含数据，实现 `IData`
- [ ] DataCenter 继承 `DataCenter<T>`，没有重复实现字典
- [ ] 数据中心有公共无参构造，并通过 `DataManager.Get<T>()` 自动注册
- [ ] 对外查询用 `TryGetData`，id 失效时不继续操作
- [ ] 数据只在 `Id == -1` 时入册，没有绕过基类直接改 `datas` 的 key
- [ ] 需要通知的变化都有 typed 事件，事件命名 `OnXxx`
- [ ] 接口只暴露视图 / Flow / Bond 真正需要的能力
- [ ] Manager 是实现 `IModule` 的普通 C# 类，`InterfaceType` 指向正确的业务接口
- [ ] Manager 没有继承 `MonoBehaviour`，也没有作为组件挂载到场景或预制体
- [ ] 生命周期方法都是 `public`，销毁钩子写的是 `OnRemove()`
- [ ] 订阅在 `OnAwake`，用命名方法，在 `OnRemove()` 成对退订；清理逻辑幂等
- [ ] 动手前已检查内置模块，没有重复实现已有能力
- [ ] 静态数据只加在 `GameConfig.cs`，每个字段注释了用途
- [ ] PM 中只有与 Module、DataCenter、Bond、Flow、Render、UI 无关的纯静态工具方法
- [ ] 公用枚举只加在 `GameEnum.cs`
- [ ] 模块内只通过 `DataManager.X` 属性访问数据中心，没有缓存
- [ ] 模块不引用其他模块的具体类，跨模块动作由 Bond 完成
- [ ] Bond 封装了模块间和模块与 Render / UI 间的组合逻辑，且没有实现流程切换
- [ ] Flow 只包含步骤编排、单模块管理、Bond 调用和流程切换，没有多模块协作或 Render / UI 细节
- [ ] Flow 的自定义方法只用于流程切换，事件在 `Exit()` 成对退订
- [ ] 模块通过 `CreateModule / GetModule / RegisterModule` 显式管理，没有依赖场景自动扫描
- [ ] `Doc/README.md` 已写，成员名与代码一致
- [ ] Unity 编译通过，Play 后模块只注册一次、生命周期日志正常
- [ ] 模块单独复制到空项目时不会引用其他业务模块

---

## 6. 反模式速查

| 反模式 | 后果 | 正确做法 |
|---|---|---|
| Flow 同时协调多个模块，或直接操作 Render / UI | Flow 重新变成跨层粘合者和上帝类 | 多模块与跨层组合迁入 Bond；单模块管理可留在 Flow |
| Flow 编写卡牌/单位转换、实体生成或 UI 刷新方法 | 其他流程无法复用，流程类越来越长 | 整体迁入 Bond |
| Bond / Module / Render / UI 调用 `GF.flow.SwitchFlow` | 流程控制权泄漏，切换规则散落 | 发事件或返回结果，只在 Flow 中切换 |
| 把模块相关公共方法放进 PM | PM 再次膨胀为业务上帝类 | 单领域方法放 Module，跨模块/跨层方法放 Bond |
| 在 Render/UI 里 `new XxxManager()` | 模块不受 GF 管理，生命周期失效 | 按职责使用 `GF.GetModule<XxxManager>()` 或 Bond |
| `GF.GetModule<IInventoryManager>()` | 泛型约束不满足，编译失败 | 用具体类型 |
| 模块继承 `MonoBehaviour` 或挂载到场景 | 混入两套生命周期，GF 会拒绝创建或注册 | 改为普通 C# 类；Unity 消息和场景对象交给 Render |
| 把 `OnDestroy()` 当成 `IModule` 销毁钩子 | GF 的清理不会执行 | 写 `public void OnRemove()` |
| 在 Unity `Awake` 里订阅数据中心 | 时序未定义 | 移到 `OnAwake` |
| lambda 订阅但不保存委托 | 无法退订 | 命名方法 + 成对 `-=` |
| 缓存 `DataManager.X` | `PreWarm` 替换实例后引用失效 | 每次用属性取 |
| 直接返回内部 `Dictionary` | 调用方绕过事件改数据 | 返回拷贝、只读视图或查询方法 |
| 手动分配或缓存数据中心 Code | 自动注册顺序变化后跨中心串号 | 通过 `Get<T>()` 注册；Code 仅作当前运行时句柄 |
| 视图和 Bond 重复 `Register` | 一个实体两条数据 | 只保留 Bond 一个注册写入者；静态场景兜底也复用 Bond |
| 把 id 当小整数解析/硬编码 | 合成 id 生效后逻辑失效 | id 当不透明句柄，展示用 `ToLocalId` |
| 模块直接 `GF.GetModule<OtherManager>()` | 模块耦合，无法单独搬运 | 交给 Bond 组合 |
| 用 Unity `Update()` 跑模块逻辑 | 绕开逻辑帧 | `OnUpdate` |
| 在框架 `Architecture/` 写具体业务 Bond | 框架层被业务污染 | 业务 Bond 放游戏侧 `Bond/` 目录 |
| 数据中心里写业务规则 | 数据和逻辑耦合 | 规则放模块 |
| 新建 `Utils / Helper / Common / Extensions` | 工具类遍地 | 纯工具放 PM；业务粘合放 Bond |
| 新建配置类 / 常量类 | 配置分裂 | 静态数据只加 `GameConfig.cs` |
| 在模块 Data 里重复定义跨模块枚举 | 同语义多份枚举 | 公用枚举放 `GameEnum.cs` |
| 不查内置模块就新建模块 | 重复造轮子 | 先看清单，有就调用或扩展 |
