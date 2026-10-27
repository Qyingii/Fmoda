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

三条设计原则（来自 `Assets/Example/README/README.md`）：

1. **模块解耦**：模块可以整体搬到另一个项目而不报错。
2. **顺序可控**：启动、更新、销毁的顺序由 GF / Flow / Priority 显式决定，不依赖 Unity 消息顺序。
3. **数据逻辑分离**：数据只描述状态，规则和事件在模块里。

---

## 2. 概念 → 文件地图

| 概念 | 位置 | 职责 |
|---|---|---|
| GF | `Assets/FModa/Architecture/Global/GF.cs` | 单例、逻辑帧、模块注册/获取/移除、场景模块自动注册、`Spawn/Despawn` |
| GameConfig | `Assets/FModa/Architecture/Global/GameConfig.cs` | 全局配置，实例在 `Assets/Resources/GameConfig.asset` |
| Flow 契约 | `Assets/FModa/Architecture/Flows/IFlow.cs` | `Enter / Update / Exit`，均有默认空实现 |
| Flow 管理 | `Assets/FModa/Architecture/Flows/FlowManager.cs` | `StartFlow / SwitchFlow`，切换时先调旧流程 `Exit()` |
| Module 契约 | `Assets/FModa/Modules/IModule.cs` | `InterfaceType / Priority / OnAwake / OnStart / OnUpdate / OnLateUpdate / OnDestroy / OnApplicationQuit` |
| 数据标记 | `Assets/FModa/Architecture/Data/IData.cs` | 空标记接口 |
| 数据中心 | `Assets/FModa/Architecture/Data/DataCenter.cs`、`IDataCenter.cs` | 泛型 `Dictionary<long,T>` 容器、自增 id、增删改查 |
| 数据中心注册 | `Assets/FModa/Architecture/Data/DataManager.cs` | 静态懒加载属性，所有 DataCenter 的唯一入口 |
| UI 基类 | `Assets/FModa/Architecture/UI/UIBase.cs` | `OnBind / OnUnbind / OnRefresh`、子 UI |
| UI 管理 | `Assets/FModa/Architecture/UI/UIManager.cs` | `OpenUI / CloseUI`、复用、延迟销毁 |
| 对象池 | `Assets/FModa/Architecture/Renders/Poolable.cs`、`PoolManager.cs` | `GF.Spawn / GF.Despawn` 的底层 |
| 内置模块 | `Assets/FModa/Modules/{Input,Network,Modding,Hotfix}/` | 可参考的标准模块结构 |
| 示例纵切 | `Assets/Example/Scripts/` | Unit 模块、Flow、Render、UI 的完整参考实现 |

---

## 3. 第一次读仓库：按这个顺序

不要从 `Assets/Example` 随便点开一个文件开始读。按下面的顺序，先建立骨架，再看血肉。

1. `Assets/Example/README/README.md`
   - 读设计意图和术语。**只信结构，不轻信成员名**，里面有已知的过期写法（见第 5 节）。
2. `Assets/FModa/Architecture/Global/GF.cs`
   - 重点看 `Awake()` 的初始化顺序、`Update()` 的逻辑帧派发、`RegisterModule / CreateModule / GetModule / RemoveModule`、`AutoRegisterSceneModules()`。
   - 读完后应该能回答：模块什么时候被创建？谁调用 `OnAwake`？逻辑帧多长？场景里的模块为什么不需要手写注册？
3. `Assets/FModa/Modules/IModule.cs` 和 `Assets/FModa/Architecture/Flows/IFlow.cs`
   - 两个契约都很短，先看清默认实现，避免以为「没写方法就是没调用」。
4. `Assets/FModa/Architecture/Data/DataCenter.cs` + `DataManager.cs`
   - 看清 `protected datas`、`protected id`、`GetData` 返回引用、`AddData` 自增 id。
   - 重点确认：**当前基类并没有 `OnDataAdded / OnDataRemoved / OnCleared` 事件**，不要照着 README 直接订阅一个不存在的事件。
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

1. `Assets/Example/Scripts/Renders/Unit/Units.cs:41` — 渲染器轮询输入 `inputManager.KeyDown("UnitDeductionOfBlood")`
2. `Assets/Example/Scripts/Modules/Unit/UnitManager.cs:87` — 调用模块的 `TakeDamage(id)`
3. `Assets/Example/Scripts/Modules/Unit/Data/UnitDataCenter.cs:17` — 模块通过 `UpdateUnitData` 修改数据中心里的引用
4. `UnitManager.cs:90` — 模块广播 `OnHpChanged`
5. `Assets/Example/Scripts/UI/UnitHpPanel.cs:27` — UI 在 `OnBind` 里订阅，`RefreshAll` 从模块的 `GetAllUnitData()` 重新取数据
6. `UnitHpPanel.cs:65` — 模块再把 DataCenter 的字典交给 UI 做展示

这条链路里，Render 和 UI 都没有直接碰 `DataManager.Unit`。**如果你追踪到的链路中视图直接访问了 DataCenter，那不是另一条合法路径，而是需要修的越界。**

---

## 4. 代码归属速查

| 你要写的东西 | 应该放在 |
|---|---|
| 全局配置、可调参数、静态只读表 | `GameConfig` 或新的 `ScriptableObject`，不要塞进 DataCenter |
| 会被增删改、需要按 id 查询的运行时状态 | `DataCenter<T>` + `IData` |
| 业务规则、状态转换、对外的业务事件 | `Module`（Manager） |
| 多个模块的启动顺序、场景/关卡切换 | `Flow` |
| 每帧动画、输入轮询、Transform 操作 | `Render`（普通 `MonoBehaviour`） |
| 面板、按钮、文本刷新 | `UIBase` |
| 跨模块协作 | 写在 `Flow` 里，通过各自的接口调用 |
| 纯工具函数、无状态算法 | 普通静态类，不要为了「统一」硬造模块 |

判断口诀：**有状态且有规则 → 模块；有状态但只是数据 → 数据中心；没有状态 → 静态类/工具；只在屏幕上表现 → Render/UI。**

---

## 5. 事实优先级与已知漂移

遇到文档和代码冲突时，按这个顺序取信：

```
运行时代码 > 接口定义 > 示例代码 > README/Doc > 根目录 CLAUDE.md
```

截至本 skill 编写时，已确认的漂移：

| 文档说法 | 代码事实 | 你应该怎么做 |
|---|---|---|
| 根目录 `CLAUDE.md` 描述 `Assets/ECMVC/` 的 EC/MVC 分层、`EntityBehaviour`、`CustomComponent`、MoonSharp | `Assets/ECMVC/` 不存在，仓库中也没有 `EntityBehaviour` / `CustomComponent` / MoonSharp 代码；热更新实际用 XLua | 不要按 `CLAUDE.md` 重建 ECMVC 目录；以 `Assets/FModa` 为准 |
| README 使用 `GF.flowManager`、`GF.uiManager` | 实际静态字段是 `GF.flow`、`GF.ui`、`GF.pool`、`GF.config` | 写代码用实际字段；改文档时顺手修正 |
| README 说数据中心会广播 `OnDataAdded / OnDataRemoved / OnCleared` | 当前 `DataCenter<T>` 没有这三个事件 | 不要订阅不存在的事件；需要时在自己的 DataCenter 里显式加事件 |
| README 说 `Clear()` 会把 id 计数归零 | `DataCenter<T>.Clear()` 只清字典，注释明确写「id 不重置」 | 以代码为准 |
| README 说 `Units.cs` 订阅了数据生命周期事件 | 实际 `Units.cs` 没有订阅，只在 `OnDestroy` 里 `UnRegister` | 需要外部删除通知时，由模块补业务事件，不要假设基类已有 |
| README 要求 `IModule` 生命周期方法必须 `public` | 示例 `UnitManager.OnDestroy()` 是 private | 新代码按规范写 public；private 版本只是碰巧被 Unity 消息调用，`IModule.OnDestroy` 不会被调用 |
| `Assets/FModa/Modules/Input/Doc/README.md` 写 `UnSubscribeStarted` | 实际接口方法是 `UnsubscribeStarted`（小写 s） | 以 `IInputManager.cs` 为准 |
| `Assets/Example/README/README.md` 说不要用 lambda 订阅 | 示例 `ExampleFlow.cs` 自己用了 lambda 且没有退订 | 新代码用命名方法；示例里的写法不要照抄 |

**处理原则**：文档与代码冲突时，不要同时保留两套说法。你要改这个区域，就在同一次改动里把文档改成与代码一致，或把代码改成与设计一致，并说明选择了哪一个。

---

## 6. 动手前的硬性检查

在写第一行代码前，必须能回答：

1. **归属**：这个改动属于 Data / DataCenter / Module / Flow / Render / UI 中的哪一层？
2. **是否已存在**：用 `grep` 确认没有同职责的模块、数据中心或事件。不要新建一个并行系统。
3. **唯一写入者**：这份状态由谁写？如果两个模块都能写，先回到 Flow 层重新划分。
4. **跨边界事件**：视图需要知道什么变化？事件定义在哪个模块接口上？谁在什么地方退订？
5. **生命周期**：谁创建它？场景自动注册还是 Flow 创建？谁移除它？切场景时怎么处理？
6. **可移植性**：这个模块如果整体复制到另一个 Unity 项目，会不会因为引用了 `Assets/Example` 或别的模块而报错？

任何一条答不上来，说明还没读够，不要开始堆代码。

---

## 7. 看到这些信号就停下来

- 视图里出现 `DataManager.XXX`：越界，改为通过模块方法/事件。
- 新代码里出现 `FindObjectOfType`、新的 `Instance` 单例来绕开 GF：在造第二套框架。
- 模块 A 直接 `GF.GetModule<BManager>()` 并调用具体方法：耦合过紧，交给 Flow 或依赖接口。
- 在 Unity 的 `Awake()` 里订阅模块/数据中心：GF 与场景对象的 `Awake` 顺序未定义，订阅应放在模块的 `OnAwake`。
- `void OnDestroy()` 写成 private，却指望 `IModule.OnDestroy` 被调用：接口方法不会被映射。
- 在模块的每帧 `Update()` 里做逻辑：模块逻辑应走 `OnUpdate`（默认 0.1s 逻辑帧）；Render 才允许每帧。
- 场景里已经挂了模块，又手写 `GF.RegisterModule<T>()`：会注册两次，生命周期跑两遍。
- 写 `GF.GetModule<IUnitManager>()`：业务接口不继承 `IModule`，编译不过；应写具体类型 `GF.GetModule<UnitManager>()`。
- 用 lambda 订阅事件，却没有保存引用用于退订：`-=` 匹配不上。
- 把 `DataManager.Unit` 缓存到字段：`DataManager.PreWarm()` 会整体替换实例，缓存变成孤儿。
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

**先找模块，再找数据；视图只喊话，模块做决定，Flow 管顺序，GF 管生死。**
