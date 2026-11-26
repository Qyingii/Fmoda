---
name: fmoda-architecture
description: FModa/ECMVC Unity 项目的架构地图与代码阅读指南。当需要理解项目分层、判断新代码应该放在哪一层、追踪一个功能从输入到 UI 的完整链路、或修改前确认现有实现时使用。包含概念到文件的映射、推荐阅读顺序、文档与代码冲突时的处理规则，以及必须停止的架构反模式。
---

# FModa 架构阅读指南

本 skill 的目标是让后来者**先读懂、再动手**。项目不是普通的 Unity MVC，而是一套「GF 持有模块 → Flow 编排模块 → Module 独占数据 → Render/UI 只调用模块」的数据流架构。凡是不按这条链路写的代码，即使能跑，也属于需要返工的代码。

> 本 skill 基于 2026-10-27 的代码快照。代码会变，**永远以运行时代码为准**；如果发现本 skill 与代码不一致，先信代码，并在改动中顺手修正文档。

---

## 1. 架构全貌

```
GF（场景中的 GameManager，单例 + DontDestroyOnLoad）
├── config   : GameConfig（ScriptableObject）
├── flow     : FlowManager ── 全局同时只有一个 CurrentFlow
├── ui       : UIManager
├── pool     : PoolManager
└── 模块注册表：IModule 实例

Flow ──启动 / 销毁──> Module ──唯一允许直接访问──> DataCenter<T> ──> Data
                      ↑
Render / UI ──只能通过──> Module
```

依赖方向是**单向**的：

```
Render / UI  →  Module  →  DataCenter  →  Data
Flow         →  Module
GF           →  Flow / UI / Pool / Module 注册表
```

禁止方向：

- `Render / UI → DataCenter`：视图直接读数据中心，等于绕过了模块这一层，数据变化就没有统一的转发点。
- `Module A → Module B 的具体实现`：模块之间不互相引用，跨模块协作交给 Flow 编排。
- `Data → 任何逻辑`：数据结构只存字段，不持有 MonoBehaviour、不查数据库、不调用模块。
- `Flow → UI 细节`：Flow 只决定「启动谁、何时切换」，不操作具体控件。

三条设计原则（来自 `Assets/FModa/README/README.md`）：

1. **模块解耦**：模块可以整体搬到另一个项目而不报错。
2. **顺序可控**：启动、更新、销毁的顺序由 GF / Flow / Priority 显式决定，不依赖 Unity 消息顺序。
3. **数据逻辑分离**：数据只描述状态，规则和事件在模块里。

---

## 2. 概念 → 文件地图

| 概念 | 位置 | 职责 |
|---|---|---|
| GF | `Assets/FModa/Architecture/Global/GF.cs` | 单例、逻辑帧、模块注册/获取/移除、场景模块自动注册、`Spawn/Despawn`、`Log/LogWarning/LogError`（受 `debugMode` 控制） |
| GameConfig | `Assets/FModa/Architecture/Global/GameConfig.cs` | **全局静态数据的唯一位置**（配置、常量、只读表）；实例在 `Assets/Resources/GameConfig.asset`，通过 `GF.config` 读取，每个字段必须注释用途 |
| PM | `Assets/FModa/Architecture/Global/PM.cs` | **公用静态方法的唯一位置**，命名空间 `FModa`，统一 `PM.xxx(...)` |
| GameEnum | `Assets/FModa/Architecture/Global/GameEnum.cs` | **公用枚举的唯一位置**，命名空间 `FModa.Enum` |
| Flow 契约 | `Assets/FModa/Architecture/Flows/IFlow.cs` | `Enter(IFlow lastFlow, List<object> list = null) / Update / Exit`，三个方法都**没有默认实现**，必须写全 |
| Flow 管理 | `Assets/FModa/Architecture/Flows/FlowManager.cs` | `StartFlow / SwitchFlow / SwitchSceneAndFlow / SwitchSceneAndFlowAsync`；切换时先调旧流程 `Exit()`，`Enter` 收到上一流程 |
| Module 契约 | `Assets/FModa/Modules/IModule.cs` | `InterfaceType / Priority / OnAwake / OnStart / OnUpdate / OnLateUpdate / OnRemove / OnApplicationQuit`；接口方法都有空默认实现，其中**销毁钩子是 `OnRemove`，不是 `OnDestroy`** |
| 数据标记 | `Assets/FModa/Architecture/Data/IData.cs` | 空标记接口 |
| 数据中心 | `Assets/FModa/Architecture/Data/DataCenter.cs`、`IDataCenter.cs` | 泛型 `Dictionary<long,T>` 容器、合成 id（Code + 本地序号）、增删改查 |
| id 布局 | `Assets/FModa/Architecture/Data/DataCenterId.cs` | 12 位数据中心 Code + 51 位本地 id、Code 占用校验、`Compose/Local/Center/Describe` |
| 数据中心注册 | `Assets/FModa/Architecture/Data/DataManager.cs` | 静态懒加载属性，所有 DataCenter 的唯一入口 |
| UI 基类 | `Assets/FModa/Architecture/UI/UIBase.cs` | `OnBind / OnUnbind / OnRefresh`、子 UI |
| UI 管理 | `Assets/FModa/Architecture/UI/UIManager.cs` | `OpenUI / CloseUI`、复用、延迟销毁 |
| 对象池 | `Assets/FModa/Architecture/Renders/Poolable.cs`、`PoolManager.cs` | `GF.Spawn / GF.Despawn` 的底层 |
| 场景对象管理 | `Assets/FModa/Architecture/Renders/GameObjectManager.cs`、`Sgo.cs`、`ISGO.cs` | `GF.sgo`：预制体引用与场景对象按 ID 查询；场景对象挂 `Sgo`（或实现 `ISGO`），和 GF 挂在同一 GameObject |
| 内置模块 | `Assets/FModa/Modules/{Input,Network,Hotfix,Modding,Multilingual,Debug,Pathfinding}/` | **先复用、再考虑新建**；清单和用途见 `Skills/README.md` 的「内置模块：先查再写」 |
| 示例纵切 | `Assets/Example/Scripts/` | Unit 模块、Flow、Render、UI 的完整参考实现（教学示例，不是内置服务） |

> 新增任何类型前，先确认 `Assets/FModa/Modules/` 没有现成能力。静态数据 / 静态方法 / 公用枚举分别只能放进 `GameConfig.cs` / `PM.cs` / `GameEnum.cs`，这是硬约束，详见 `Skills/README.md`。

---

## 3. 第一次读仓库：按这个顺序

不要从 `Assets/Example` 随便点开一个文件开始读。按下面的顺序，先建立骨架，再看血肉。

1. `Assets/FModa/README/README.md`
   - 读设计意图和术语。**只信结构，不轻信成员名**，里面有已知的过期写法（见第 5 节）。旧的 `Assets/Example/README/README.md` 已移动到此处。
2. `Assets/FModa/Architecture/Global/GF.cs`
   - 重点看 `Awake()` 的初始化顺序、`Update()` 的逻辑帧派发、`RegisterModule / CreateModule / GetModule / RemoveModule`、`AutoRegisterSceneModules()`。
   - 读完后应该能回答：模块什么时候被创建？谁调用 `OnAwake`？逻辑帧多长？场景里的模块为什么不需要手写注册？
3. `Assets/FModa/Modules/IModule.cs` 和 `Assets/FModa/Architecture/Flows/IFlow.cs`
   - `IModule` 的每个方法都有空默认实现（注意销毁钩子是 `OnRemove`）；`IFlow` 的 `Enter / Update / Exit` **没有**默认实现，必须全部写出来。
   - 看清签名：`IFlow.Enter(IFlow lastFlow, List<object> list = null)`，第一个参数是上一流程，首次启动为 `null`。
4. `Assets/FModa/Architecture/Data/DataCenter.cs` + `DataManager.cs` + `DataCenterId.cs`
   - 看清 `protected datas`、`protected id`、`GetData` 返回引用、`AddData` 自增 id。
   - 存储 key 是「Code + 本地 id」的合成 long；Code 在 `DataManager.DataCenterCodes` 中分配，Code 0 等于旧行为。
   - 只有 `Id == -1` 的数据能 `AddData`，离开中心后 Id 会失效为 -1；查询优先用 `TryGetData`。
   - 重点确认：**当前基类并没有 `OnDataAdded / OnDataRemoved / OnCleared` 事件**，不要照着旧版文档直接订阅一个不存在的事件。
5. `Assets/FModa/Architecture/UI/UIBase.cs` + `UIManager.cs`
   - 重点看 `OnEnable / OnDisable / OnDestroy` 如何驱动 `OnBind / OnUnbind / OnRefresh`，以及关闭的 UI 为什么不会立刻销毁。
6. `Assets/FModa/Architecture/Renders/Poolable.cs` + 第 2 节里的对象池调用点
   - 重点看 `ActionOnRelease` 为什么必须重置状态。
7. `Assets/Example/Scripts/Modules/Unit/` 全套
   - 这是唯一一条从「数据 → 数据中心 → 模块 → 接口」的完整纵切。
8. `Assets/Example/Scripts/Flows/ExampleFlow.cs`、`Renders/Unit/Units.cs`、`UI/UnitHpPanel.cs`
   - 看清 Flow 如何启动模块、Render 如何注册数据、UI 如何订阅模块事件。
9. 最后才读你要修改的目标模块。

### 读一个陌生模块的顺序

1. **先读接口** `Interface/I*.cs`：这是模块对外的全部契约，事件和方法都在这里。如果没有接口直接进入第2步。
2. **再读 Manager**：谁实现了接口、`InterfaceType` 指向谁、`Priority` 是多少、生命周期里做了什么、访问了哪个 DataCenter。
3. **再读 Data + DataCenter**：状态长什么样、有没有语义方法、有没有自己的 typed 事件。
4. **读 `Doc/README.md`**：了解作者的意图，但成员名以代码为准。
5. **最后用搜索确认调用方**：`grep -rn "GetModule<XxxManager>\|XxxManager" Assets --include=*.cs`。
6. 回答四个问题：谁创建它？谁调用 `Init()`？谁订阅它的事件？谁移除它？

### 追踪一条功能链路的范例

以「扣血」为例，标准链路是：

1. `Assets/Example/Scripts/Renders/Unit/Units.cs:61` — 渲染器轮询输入 `inputManager.KeyDown("UnitDeductionOfBlood")`
2. `Assets/Example/Scripts/Modules/Unit/UnitManager.cs:93` — 调用模块的 `TakeDamage(id)`
3. `Assets/Example/Scripts/Modules/Unit/Data/UnitDataCenter.cs:19` — 模块通过 `UpdateUnitData` 修改数据中心里的引用
4. `UnitManager.cs:99` — 模块广播 `OnHpChanged`
5. `Assets/Example/Scripts/UI/UnitHpPanel.cs:27` — UI 在 `OnBind` 里订阅，`RefreshAll` 从模块的 `GetAllUnitData()` 重新取数据
6. `UnitHpPanel.cs:77` — 模块再把 DataCenter 的字典交给 UI 做展示

这条链路里，Render 和 UI 都没有直接碰 `DataManager.Unit`。**如果你追踪到的链路中视图直接访问了 DataCenter，那不是另一条合法路径，而是需要修的越界。**

---

## 4. 代码归属速查

| 你要写的东西 | 应该放在 |
|---|---|
| 全局配置、可调参数、常量、静态只读表 | **只放 `Assets/FModa/Architecture/Global/GameConfig.cs`**，通过 `GF.config` 读取；每个字段注释用途。不要新建 `ScriptableObject` 子类或常量类，也不要塞进 DataCenter |
| 会被增删改、需要按 id 查询的运行时状态 | `DataCenter<T>` + `IData` |
| 业务规则、状态转换、对外的业务事件 | `Module`（Manager），但先确认内置模块没有同类能力 |
| 多个模块的启动顺序、场景/关卡切换 | `Flow` |
| 每帧动画、输入轮询、Transform 操作 | `Render`（普通 `MonoBehaviour`） |
| 面板、按钮、文本刷新 | **`UIBase` 子类（不能直接 `MonoBehaviour`）**，预制体放 `Resources/UI`，通过 `GF.ui.OpenUI` 打开 |
| 场景内对象 / 预制体引用 | `GameObjectManager` + `GF.sgo`；场景对象挂 `Sgo`（或实现 `ISGO`），预制体引用登记在 Inspector 字段 |
| 需要频繁生成 / 回收的对象 | `Poolable` + `GF.Spawn / GF.Despawn` |
| 跨模块协作 | 写在 `Flow` 里，通过各自的接口调用 |
| 公用静态方法、纯工具函数、无状态算法 | **只放 `Assets/FModa/Architecture/Global/PM.cs`**，统一 `PM.xxx(...)`；不要新建 `Utils/Helper/Common` 类 |
| 会被两个及以上模块/视图使用的枚举 | **只放 `Assets/FModa/Architecture/Global/GameEnum.cs`**（`FModa.Enum`），不要在模块 Data 里重复定义 |
| 需求看起来像内置模块已经做过的事 | 先读 `Skills/README.md` 的内置模块清单，调用或扩展现有模块 |

判断口诀：**先翻内置模块 → 有状态且有规则才做模块；有状态但只是数据 → 数据中心；全局静态数据 → GameConfig；公用静态方法 → PM；公用枚举 → GameEnum；只在屏幕上表现 → Render/UI。**

---

## 5. 事实优先级与已知漂移

遇到文档和代码冲突时，按这个顺序取信：

```
运行时代码 > 接口定义 > 示例代码 > README/Doc > 根目录 CLAUDE.md
```

截至本 skill 编写时，已确认的漂移：

| 文档说法 | 代码事实 | 你应该怎么做 |
|---|---|---|
| README 使用 `GF.flowManager`、`GF.uiManager` | 实际静态字段是 `GF.flow`、`GF.ui`、`GF.pool`、`GF.config`、`GF.sgo` | 写代码用实际字段；改文档时顺手修正 |
| 旧版 README / 部分 Doc 说数据中心会广播 `OnDataAdded / OnDataRemoved / OnCleared` | 当前 `DataCenter<T>` 没有这三个事件 | 不要订阅不存在的事件；需要时在自己的 DataCenter 里显式加事件 |
| 旧版文档 / 网上示例把 `IModule` 的销毁钩子写成 `OnDestroy` | 接口方法是 **`OnRemove()`**；`OnDestroy()` 只是 MonoBehaviour 的 Unity 消息，GF 不会调用它 | 实现 `IModule` 时写 `public void OnRemove()`；MonoBehaviour 的 Unity `OnDestroy` 另行处理，两处清理逻辑共用且幂等（示例 `UnitManager` 已按此修正） |
| README 快速开始里的流程写 `public void Enter()` | 实际签名是 `Enter(IFlow lastFlow, List<object> list = null)`，且 `IFlow` 没有默认实现 | 按当前签名写；`lastFlow` 为 `null` 表示首次启动 |
| README 快速开始用 `GF.flowManager.StartFlow` / `GF.uiManager.OpenUI` | 实际是 `GF.flow.StartFlow` / `GF.ui.OpenUI` | 以 `GF.cs` 的静态字段为准 |
| `Assets/FModa/Modules/Input/Doc/README.md` 写 `UnSubscribeStarted` | 实际接口方法是 `UnsubscribeStarted`（小写 s） | 以 `IInputManager.cs` 为准 |
| README 说订阅要用命名方法 | 示例 `ExampleFlow.cs:24` 自己用了 lambda，且 `Exit()` 没有退订 | 新代码用命名方法并在 `Exit()` 成对退订；示例里的写法不要照抄 |
| 旧文档引用 `Assets/Example/README/README.md` | 该文件已移动到 `Assets/FModa/README/README.md` | 以新路径为准 |

**处理原则**：文档与代码冲突时，不要同时保留两套说法。你要改这个区域，就在同一次改动里把文档改成与代码一致，或把代码改成与设计一致，并说明选择了哪一个。

---

## 6. 动手前的硬性检查

在写第一行代码前，必须能回答：

1. **归属**：这个改动属于 Data / DataCenter / Module / Flow / Render / UI 中的哪一层？静态数据、静态方法、公用枚举是否分别落在 `GameConfig.cs` / `PM.cs` / `GameEnum.cs`？
2. **是否已存在**：先读 `Skills/README.md` 的内置模块清单，再用 `grep` 确认没有同职责的模块、数据中心或事件。不要重复造轮子，也不要新建并行系统。
3. **唯一写入者**：这份状态由谁写？如果两个模块都能写，先回到 Flow 层重新划分。
4. **跨边界事件**：视图需要知道什么变化？事件定义在哪个模块接口上？谁在什么地方退订？
5. **生命周期**：谁创建它？场景自动注册还是 Flow 创建？谁移除它？切场景时怎么处理？
6. **可移植性**：这个模块如果整体复制到另一个 Unity 项目，会不会因为引用了 `Assets/Example` 或别的模块而报错？
7. **视图入口**：UI 是否继承 `UIBase` 并通过 `GF.ui.OpenUI` 打开？场景对象 / 预制体是否通过 `GF.sgo`（`Sgo`/`ISGO`）获取，而不是 `GameObject.Find` / `FindObjectOfType`？

任何一条答不上来，说明还没读够，不要开始堆代码。

---

## 7. 看到这些信号就停下来

- 视图里出现 `DataManager.XXX`：越界，改为通过模块方法/事件。
- 新代码里出现 `FindObjectOfType`、新的 `Instance` 单例来绕开 GF：在造第二套框架。
- 模块 A 直接 `GF.GetModule<BManager>()` 并调用具体方法：耦合过紧，交给 Flow 或依赖接口。
- 在 Unity 的 `Awake()` 里订阅模块/数据中心：GF 与场景对象的 `Awake` 顺序未定义，订阅应放在模块的 `OnAwake`。
- 实现 `IModule` 时写 `void OnDestroy()`：接口的销毁钩子是 **`OnRemove()`**，GF 调不到 `OnDestroy`；Unity 的 `OnDestroy` 消息与它无关。
- 新建 `Utils` / `Helper` / `Common` / `Extensions` 静态工具类，或另造 `public static` 数据类：公用静态方法只放 `PM.cs`，静态数据只放 `GameConfig.cs`。
- 在模块 Data 里新增跨模块枚举，再写转换适配器：公用枚举只放 `GameEnum.cs`。
- 新建自己的 `ScriptableObject` 配置类或常量类：静态数据统一进 `GameConfig.cs`（`GameConfig` 实例在 `Resources/GameConfig.asset`）。
- 没翻 `Assets/FModa/Modules/` 就新建模块：先确认内置模块没有现成能力，再考虑新模块。
- 让 UI 面板直接继承 `MonoBehaviour`：`UIManager` 只认 `UIBase`，`OpenUI` 会报「该UI没有UIBase组件」并返回 `null`。
- 手动 `Instantiate` UI 或自己注册：绕过 `UIManager` 的打开 / 关闭 / 复用体系，面板不受管理。
- 用 `GameObject.Find` / `FindObjectOfType` 找场景对象：改名、复制、失活、换场景都会失效；用 `Sgo` + `GF.sgo`。
- 在模块的每帧 `Update()` 里做逻辑：模块逻辑应走 `OnUpdate`（默认 0.1s 逻辑帧）；Render 才允许每帧。
- 场景里已经挂了模块，又手写 `GF.RegisterModule<T>()`：会注册两次，生命周期跑两遍。
- 写 `GF.GetModule<IUnitManager>()`：业务接口不继承 `IModule`，编译不过；应写具体类型 `GF.GetModule<UnitManager>()`。
- 用 lambda 订阅事件，却没有保存引用用于退订：`-=` 匹配不上。
- 把 `DataManager.Unit` 缓存到字段：`DataManager.PreWarm()` 会整体替换实例，缓存变成孤儿。
- 视图自己 `Register`，Flow 又 `Register` 一次：一个实体两条数据，id 对不上；注册只保留一个写入者。
- 把数据中心 id 当小整数用（`if (id == 1)`、直接 `ToString` 展示）：id 是「Code + 本地序号」的合成值，展示要用 `ToLocalId`。
- 在 `Assets/FModa/Architecture/` 里加业务代码：这里是框架层，业务模块放 `Assets/FModa/Modules/` 或游戏自己的目录。

---

## 8. 验证方式

这个项目没有 CLI 构建脚本，验证在 Unity Editor 里完成：

1. 用 Unity 2022.3.38f1 打开项目，确认 Console 无编译错误。
2. 打开 `Assets/Example/Scenes/SampleScene.unity` 进入 Play：
   - `GF` 初始化日志正常；
   - 场景模块只注册一次；
   - 流程进入/退出日志成对；
   - UI 打开、关闭、再次打开能复用。
3. 如有测试，打开 **Window > General > Test Runner** 运行；CLI 可用：
   `Unity -runTests -testPlatform editmode -projectPath <path>`。

---

## 9. 一句话总结

**先翻内置模块，再决定造不造；先找模块，再找数据；视图只喊话，模块做决定，Flow 管顺序，GF 管生死。**
