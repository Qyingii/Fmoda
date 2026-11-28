---
name: fmoda-architecture
description: FModa/ECMVC Unity 项目的架构地图与代码阅读指南。当需要理解项目分层、判断新代码应该放在哪一层、追踪功能链路或修改前确认现状时使用。重点约束 Flow 编排步骤、管理单个模块和切换流程，Bond 负责跨模块与跨层粘合，PM 只放与模块无关的纯静态工具方法。
---

# FModa 架构阅读指南

本 skill 的目标是让后来者**先读懂、再动手**。项目采用「GF 提供全局入口 → Flow 编排步骤并管理单个模块 → Bond 完成跨模块、跨层粘合 → Module 独占数据 → Render/UI 负责表现」的结构。不要把 Flow 写成上帝类，也不要把 PM 写成业务公共方法集合。

> 本 skill 描述当前架构约束。代码会变，**永远以运行时代码和最新架构决定为准**；发现文档漂移时，应在同一次改动中修正文档。

---

## 1. 架构全貌

```text
GF（场景中的 GameManager，单例 + DontDestroyOnLoad）
├── config   : GameConfig（ScriptableObject）
├── flow     : FlowManager ── 全局同时只有一个 CurrentFlow
├── ui       : UIManager
├── pool     : PoolManager
├── sgo      : GameObjectManager
├── 模块注册表：IModule 实例
└── Bond 注册表：IBond 实例

Flow ──按步骤调用──> Bond ──组合──> Module ──唯一允许直接访问──> DataCenter<T> ──> Data
  │                    ├──────────> Render
  │                    └──────────> UI
  └────────单模块管理──────────────> Module

Render / UI ──自身显示与输入转发──> Module
Render / UI ──可复用的跨模块、跨层组合──> Bond
```

主要依赖方向：

```text
Flow         → Module（单模块管理）/ Bond
Bond         → Module / Render / UI
Render / UI  → Module（自身业务）或 Bond（跨层组合）
Module       → DataCenter → Data
GF           → Flow / Bond 注册表 / Module 注册表 / UI / Pool
```

禁止方向与越界：

- `Render / UI → DataCenter`：视图直接读数据中心会绕过模块这一层。
- `Module A → Module B 的具体实现`：模块之间不互相引用，跨模块协作交给 Bond。
- `Flow → 多模块协作 / Render / UI 细节`：Flow 可以直接创建、获取、调用和移除单个模块；涉及多个模块或模块与 Render / UI 的组合交互时，必须放进 Bond。
- `Bond / Module / Render / UI → 流程切换实现`：切换流程的实现代码只能位于 Flow。其他层只能发事件或返回结果。
- `PM → Module / DataCenter / Bond / Flow / Render / UI`：PM 只放与这些层无关的纯静态工具方法。
- `Data → 任何逻辑`：数据结构只存字段，不持有 MonoBehaviour、不查数据库、不调用模块。

三条设计原则：

1. **模块解耦**：模块可以整体搬到另一个项目而不报错。
2. **顺序可控**：Flow 显式排列业务步骤；模块生命周期由 GF、Flow 和 Bond 按职责处理，并受 Priority 约束，不依赖 Unity 消息顺序。
3. **数据逻辑分离**：数据只描述状态，规则和事件在模块里。

---

## 2. Bond / Flow / PM 的职责边界

| 层 | 应该负责 | 不应该负责 |
|---|---|---|
| **Bond** | 模块间协作；模块与 Render / UI 的组合；卡牌变单位、单位变卡牌；生成实体、注册数据、注入 id、同步视图等可复用步骤；必要时封装业务性的模块初始化与清理 | 决定整个流程顺序；实现流程切换；替代模块内部业务规则 |
| **Flow** | 实现 `Enter / Update / Exit`；按顺序执行单模块操作并调用 Bond；直接创建、获取、初始化、调用和移除单个模块；订阅流程相关事件；在自己的切换处理方法中调用 `GF.flow.SwitchFlow` 等 API | 多模块协作；跨层转换；Render / UI 细节；与切换无关的自定义帮助方法 |
| **PM** | 数值计算、纯格式化、无状态算法等静态工具方法 | 访问模块或数据中心；操作实体和视图；流程控制；任何模块相关的公共业务方法 |

**“多个流程都要用”不意味着放进 PM。** 如果方法涉及模块、实体、Render 或 UI，应放 Bond；只有与这些概念完全无关的纯工具函数才放 PM。

---

## 3. 概念 → 文件地图

| 概念 | 位置 | 职责 |
|---|---|---|
| GF | `Assets/FModa/Architecture/Global/GF.cs` | 单例、逻辑帧、普通 C# 模块和 Bond 注册/获取、`Spawn/Despawn`、日志 |
| GameConfig | `Assets/FModa/Architecture/Global/GameConfig.cs` | **全局静态数据的唯一位置**；实例在 `Assets/Resources/GameConfig.asset`，通过 `GF.config` 读取 |
| PM | `Assets/FModa/Architecture/Global/PM.cs` | **纯静态工具方法的唯一位置**，命名空间 `FModa`；禁止模块相关方法 |
| GameEnum | `Assets/FModa/Architecture/Global/GameEnum.cs` | **公用枚举的唯一位置**，命名空间 `FModa.Enum` |
| Bond 契约 | `Assets/FModa/Architecture/Bond/IBond.cs` | 粘合剂标记接口；具体 Bond 按业务领域实现 |
| Bond 管理 | `Assets/FModa/Architecture/Global/GF.cs` 的「粘合剂管理」区域 | `CreateBond<T>()` 显式创建；`GetBond<T>()` 获取，不存在时自动创建；按具体类型复用 |
| Flow 契约 | `Assets/FModa/Architecture/Flows/IFlow.cs` | `Enter(IFlow lastFlow, List<object> list = null) / Update / Exit`，三个方法都必须实现 |
| Flow 管理 | `Assets/FModa/Architecture/Flows/FlowManager.cs` | `StartFlow / SwitchFlow / SwitchSceneAndFlow / SwitchSceneAndFlowAsync` |
| Module 契约 | `Assets/FModa/Modules/IModule.cs` | 普通 C# 类实现；`InterfaceType / Priority / OnAwake / OnStart / OnUpdate / OnLateUpdate / OnRemove / OnApplicationQuit`；销毁钩子是 `OnRemove`，禁止继承 `MonoBehaviour` |
| 数据标记 | `Assets/FModa/Architecture/Data/IData.cs` | 空标记接口 |
| 数据中心 | `Assets/FModa/Architecture/Data/DataCenter.cs`、`IDataCenter.cs` | 泛型 `Dictionary<long,T>` 容器、合成 id、增删改查 |
| id 布局 | `Assets/FModa/Architecture/Data/DataCenterId.cs` | 12 位数据中心 Code + 51 位本地 id、Code 占用校验、`Compose/Local/Center/Describe` |
| 数据中心注册 | `Assets/FModa/Architecture/Data/DataManager.cs` | 静态懒加载属性，所有 DataCenter 的唯一入口 |
| UI 基类 | `Assets/FModa/Architecture/UI/UIBase.cs` | `OnBind / OnUnbind / OnRefresh`、子 UI |
| UI 管理 | `Assets/FModa/Architecture/UI/UIManager.cs` | `OpenUI / CloseUI`、复用、延迟销毁 |
| 对象池 | `Assets/FModa/Architecture/Renders/Poolable.cs`、`PoolManager.cs` | `GF.Spawn / GF.Despawn` 的底层 |
| 场景对象管理 | `Assets/FModa/Architecture/Renders/GameObjectManager.cs`、`Sgo.cs`、`ISGO.cs` | `GF.sgo`：预制体引用与场景对象按 ID 查询 |
| 内置模块 | `Assets/FModa/Modules/{Input,Network,Hotfix,Modding,Multilingual,Debug,Pathfinding,SeedRandom}/` | 先复用、再考虑新建 |
| 示例纵切 | `Assets/Example/Scripts/` | Unit 模块、UnitBond、Flow、Render、UI 的参考实现 |

> 新增任何类型前，先确认 `Assets/FModa/Modules/` 没有现成能力。静态数据、纯静态工具方法、公用枚举分别只能放进 `GameConfig.cs` / `PM.cs` / `GameEnum.cs`；跨模块和跨层业务粘合放 Bond。

---

## 4. 第一次读仓库：按这个顺序

1. `Assets/FModa/README/README.md`
   - 读设计意图、分层和术语；成员名以代码为准。
2. `Assets/FModa/Architecture/Global/GF.cs`
   - 看 `Awake()` 初始化顺序、普通 C# 模块的显式注册与生命周期，以及「粘合剂管理」区域的 `CreateBond / GetBond`。
3. `Assets/FModa/Modules/IModule.cs`、`Architecture/Bond/IBond.cs`、`Architecture/Flows/IFlow.cs`
   - 分清模块生命周期、Bond 标记契约和 Flow 三个固定方法。
4. `Assets/FModa/Architecture/Data/DataCenter.cs` + `DataManager.cs` + `DataCenterId.cs`
   - 看清 `datas`、合成 id、`AddData`、`TryGetData` 和 id 失效规则。
   - 当前基类没有 `OnDataAdded / OnDataRemoved / OnCleared` 事件。
5. `Assets/FModa/Architecture/UI/UIBase.cs` + `UIManager.cs`
   - 看 `OnBind / OnUnbind / OnRefresh` 与 UI 复用。
6. `Assets/FModa/Architecture/Renders/Poolable.cs`
   - 看 `ActionOnRelease` 的状态重置。
7. `Assets/Example/Scripts/Modules/Unit/` 全套
   - 看数据、数据中心、模块和接口的完整纵切。
8. `Assets/Example/Scripts/Bond/Unit/UnitBond.cs`
   - 看 Bond 如何组合模块注册、对象生成、id 注入和 UI 刷新。
9. `Assets/Example/Scripts/Bond/SwitchFlow/SwitchFlowBond.cs` + `Flows/ExampleFlow.cs`
   - 看 Bond 如何只发出结束请求，以及 Flow 如何自己完成切换。
10. `Renders/Unit/Units.cs`、`UI/UnitHpPanel.cs`
    - 看视图如何转发输入、订阅模块事件，以及何时调用 Bond。
11. 最后读要修改的目标模块。

### 读一个陌生模块的顺序

1. 先读 `Interface/I*.cs`，了解模块契约。
2. 再读 Manager，确认 `InterfaceType`、`Priority`、生命周期和数据中心。
3. 再读 Data + DataCenter。
4. 读 `Doc/README.md`，成员名以代码为准。
5. 搜索调用方；跨层组合还应继续找到对应 Bond，而不是默认由 Flow 处理。
6. 回答：谁创建它？谁初始化？谁订阅事件？谁移除它？

### 追踪一条功能链路的范例

以「扣血」为例：

1. `Renders/Unit/Units.cs` 轮询输入模块的 `UnitDeductionOfBlood`。
2. Render 调用 `UnitManager.TakeDamage(id)`。
3. `UnitManager` 通过 `UnitDataCenter` 修改数据并广播 `OnHpChanged`。
4. `UnitHpPanel` 在 `OnBind` 订阅事件，从模块查询数据并刷新。

这是视图自身输入转发，不需要 Bond。若动作变成「扣除卡牌资源 → 生成单位 → 绑定 Render → 刷新 UI」，则应由 Bond 组合，Flow 只调用这个 Bond 步骤。

---

## 5. 代码归属速查

| 你要写的东西 | 应该放在 |
|---|---|
| 全局配置、可调参数、常量、静态只读表 | **只放 `Global/GameConfig.cs`**，通过 `GF.config` 读取 |
| 会被增删改、需要按 id 查询的运行时状态 | `DataCenter<T>` + `IData` |
| 单个业务领域的规则、状态转换、业务事件 | `Module`（Manager），先确认内置模块没有同类能力 |
| 单个模块的创建、获取、初始化、调用和移除 | `Flow`，或其他实际使用者；不涉及多模块和视图时无需 Bond |
| 单模块操作与多个已经封装好的 Bond 步骤的先后顺序、流程切换 | `Flow` |
| 模块间协作、模块与 Render / UI 协作、卡牌与单位转换、生成实体并同步视图 | **Bond**，通过 `GF.GetBond<T>()` 复用 |
| 每帧动画、输入轮询、Transform 操作 | `Render` |
| 面板、按钮、文本刷新 | `UIBase` 子类，通过 `GF.ui.OpenUI` 打开 |
| 场景内对象 / 预制体引用 | `GameObjectManager` + `GF.sgo` |
| 需要频繁生成 / 回收的对象 | `Poolable` + `GF.Spawn / GF.Despawn` |
| 与模块无关的纯计算、纯格式化、无状态算法 | **只放 `Global/PM.cs`** |
| 会被两个及以上模块/视图使用的枚举 | **只放 `Global/GameEnum.cs`** |

判断口诀：**单领域规则进 Module；单模块管理可进 Flow；跨模块、跨层组合进 Bond；步骤顺序和流程切换进 Flow；纯工具进 PM；数据进 DataCenter；表现进 Render/UI。**

---

## 6. 事实优先级与已知漂移

遇到文档和代码冲突时，按这个顺序取信：

```text
运行时代码与最新架构决定 > 接口定义 > 示例代码 > README/Doc > 根目录 CLAUDE.md
```

已确认的漂移与注意事项：

| 旧说法或旧示例 | 当前事实 / 规则 | 你应该怎么做 |
|---|---|---|
| Flow 负责启动/销毁模块、模块间数据交换 | GF 提供底层模块生命周期；Flow 可以管理单个模块；多模块与跨层协作由 Bond 封装 | 单模块管理可留在 Flow；多模块或视图粘合迁入 Bond |
| 模块可以继承 `MonoBehaviour`、挂到场景后自动注册 | 该能力已移除；模块只能是普通 C# 类，由 `CreateModule / GetModule / RegisterModule` 显式管理 | 删除模块组件和场景引用；Unity 消息与场景对象交给 Render / `GF.sgo` |
| 此前文档误把 `ExampleFlow.cs` 的 `CreateModule / RemoveModule` 也列为待迁移残留 | 这些是允许的单模块管理，不需要迁移；`UnitBond` 与 `Units` 的注释应与实际注册者一致 | 保留合法调用，按实际注册者修正注释；lambda 未退订是另一个独立问题 |
| 跨模块协作写在 Flow | 跨模块协作统一放 Bond | Flow 只保留单模块管理和组合步骤的调用顺序 |
| 公共方法都放 PM | PM 只放与模块无关的纯静态工具方法 | 模块相关公共方法放对应模块或 Bond |
| 文档使用 `GF.flowManager`、`GF.uiManager` | 实际字段是 `GF.flow`、`GF.ui`、`GF.pool`、`GF.config`、`GF.sgo` | 使用实际字段 |
| 旧文档说数据中心有 `OnDataAdded / OnDataRemoved / OnCleared` | 当前 `DataCenter<T>` 没有这三个事件 | 需要时在具体 DataCenter 中显式定义 |
| 把 `IModule` 销毁钩子写成 `OnDestroy` | 接口方法是 `OnRemove()`；模块没有 Unity 消息 | 实现 `public void OnRemove()` |
| 流程写 `public void Enter()` | 实际签名是 `Enter(IFlow lastFlow, List<object> list = null)` | 按当前签名实现 |
| 文档写 `UnSubscribeStarted` | 输入接口实际方法是 `UnsubscribeStarted` | 以 `IInputManager.cs` 为准 |
| 示例 `ExampleFlow.cs` 使用 lambda 订阅且未在 `Exit()` 退订 | 事件应使用命名方法并成对退订 | 新代码不要照抄该写法 |
| 旧文档引用 `Assets/Example/README/README.md` | 详细框架说明位于 `Assets/FModa/README/README.md` | 使用新路径 |

**处理原则**：不要同时保留两套职责说法。发现旧代码残留时，新代码仍按 Bond / Flow / PM 边界实现，并在修改对应代码时清理残留。

---

## 7. 动手前的硬性检查

1. **归属**：改动属于 Data / DataCenter / Module / Bond / Flow / Render / UI 哪一层？
2. **是否已存在**：是否已有内置模块或 Bond 能复用，避免并行系统？
3. **唯一写入者**：状态由哪个模块写？跨模块动作是否由 Bond 协调，而没有制造第二个状态拥有者？
4. **Flow 纯度**：Flow 是否只包含步骤编排、单模块管理、Bond 调用和流程切换，没有多模块协作或视图细节？
5. **Bond 边界**：跨模块、跨层代码是否可复用且集中在 Bond？Bond 是否没有实现流程切换？
6. **PM 纯度**：方法是否完全不依赖模块、数据中心、流程、Bond 和视图？
7. **跨边界事件**：事件定义在哪里？谁订阅、谁退订？
8. **生命周期**：模块是否为普通 C# 类，并由 GF 显式注册、Flow 的单模块管理或 Bond 中封装的跨层步骤处理？
9. **可移植性**：模块整体复制到另一个项目时，是否仍不引用其他业务模块或 `Assets/Example`？
10. **视图入口**：UI 是否继承 `UIBase` 并走 `GF.ui.OpenUI`？场景对象是否走 `GF.sgo`？

---

## 8. 看到这些信号就停下来

- Flow 出现 `GF.Spawn`、`GF.sgo` 或具体 UI 操作：视图粘合越界，迁入 Bond；`GF.GetModule / CreateModule / RemoveModule` 本身允许用于单模块管理。
- Flow 同时协调多个模块：多模块粘合越界，迁入 Bond。
- Flow 出现与流程切换无关的自定义转换、生成、刷新方法：上帝类信号，迁入 Bond。
- Bond / Module / Render / UI 调用 `GF.flow.SwitchFlow`：违反死规则，改成事件或返回值，由 Flow 自己切换。
- PM 出现模块类型、`GF.GetModule`、`DataManager`、Render / UI、实体转换：不是纯工具，迁到 Module 或 Bond。
- 模块 A 直接 `GF.GetModule<BManager>()`：耦合过紧，交给 Bond。
- 模块继承 `MonoBehaviour` 或出现在场景组件中：改为普通 C# 类，并通过 GF 显式管理。
- 视图出现 `DataManager.XXX`：改为模块查询或 Bond 步骤。
- 新代码出现新的 `Instance` 单例绕开 GF：是在造第二套框架。
- 在 Unity `Awake()` 里订阅模块/数据中心：改到模块 `OnAwake`。
- 把 `IModule` 销毁钩子写成 `OnDestroy()`：改为 `public void OnRemove()`。
- 新建 `Utils / Helper / Common / Extensions`：纯工具放 PM，业务粘合放 Bond。
- 在模块 Data 里重复定义跨模块枚举：公用枚举放 `GameEnum.cs`。
- UI 面板直接继承 `MonoBehaviour`：必须继承 `UIBase`。
- 手动 `Instantiate` UI：统一走 `UIManager`。
- 用 `GameObject.Find / FindObjectOfType` 做场景装配：用 `Sgo` + `GF.sgo`。
- 在模块每帧 `Update()` 里做逻辑：模块走 `OnUpdate`，Render 才每帧。
- 写 `GF.GetModule<IUnitManager>()`：业务接口不满足约束，应写具体类型。
- 用 lambda 订阅却无法退订：改命名方法。
- 缓存 `DataManager.X`：`PreWarm()` 会替换实例。
- 同一份数据由视图和 Bond 各注册一次：只保留一个写入者。
- 把合成 id 当小整数解析或展示：需要展示时使用 `ToLocalId`。
- 在框架 `Architecture/` 中添加具体业务 Bond 或业务模块：业务代码放游戏侧目录。

---

## 9. 验证方式

1. 用 Unity 2022.3.38f1 打开项目，确认 Console 无编译错误。
2. 打开 `Assets/Example/Scenes/SampleScene.unity` 进入 Play：
   - GF 初始化日志正常；
   - 场景中不存在 Module 组件，Flow 创建的普通 C# 模块只注册一次；
   - Bond 重复获取时复用同一实例；
   - 流程进入/退出日志成对，切换只由 Flow 发起；
   - UI 打开、关闭、再次打开能复用。
3. 打开 **Window > General > Test Runner** 运行测试；CLI 可用：
   `Unity -runTests -testPlatform editmode -projectPath <path>`。

---

## 10. 一句话总结

**Module 管业务，Bond 管粘合，Flow 管步骤与切换，PM 只管纯工具，GF 提供全局生命周期与入口。**
