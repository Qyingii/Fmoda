# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## ⚠️ 强制规则：先读 Skills，再阅读或修改代码

本项目的详细架构与设计规则**不在本文件**，而在：

`Assets/FModa/Architecture/Skills/`

**在阅读、设计、修改、重构或排查本项目任何代码之前，必须完整阅读对应规则：**

| 任务 | 必读文件 |
|---|---|
| 理解架构、追踪功能、判断代码归属 | `Assets/FModa/Architecture/Skills/fmoda-architecture/SKILL.md` |
| 新增/修改模块、数据中心、Bond、Flow | `Assets/FModa/Architecture/Skills/fmoda-module-design/SKILL.md` |
| 编写 Render、UI、对象池 | `Assets/FModa/Architecture/Skills/fmoda-view-ui/SKILL.md` |

跨领域任务必须全部阅读。**未读完对应规则，不得开始设计或写代码。**

- 入口 skill：`.claude/skills/fmoda-rules/SKILL.md`
- 规则索引：`Assets/FModa/Architecture/Skills/README.md`
- 本文件只做路由；Skills 是详细规则的唯一来源。
- 代码与文档冲突时，以运行时代码为准，并在同一次改动中修正文档。

## Project Overview

**ECMVC** is a Unity 2022.3.38f1 project. The current architecture is **FModa** under `Assets/FModa/`.

> 旧版 `CLAUDE.md` 中描述的 `Assets/ECMVC/`、`EntityBehaviour`、`CustomComponent`、MoonSharp **已经不存在**。不要按旧分层重建这些目录或类型。热更新实际使用 XLua（`Assets/XLua`）。

三条设计原则：

1. **模块解耦**：模块可以整体搬到另一个项目而不报错。
2. **顺序可控**：Flow 显式编排步骤，并可管理单个模块；模块生命周期由 GF / Flow / Bond / Priority 按职责显式处理，不依赖 Unity 消息顺序。
3. **数据逻辑分离**：数据只描述状态，规则和事件在模块里。

依赖方向：

```
GF → Flow → Bond → Module → DataCenter → Data
      │        ├→ Render / UI
      └→ Module（单模块管理）
Render / UI → Module        （自身显示与输入转发；绝不直接访问 DataCenter）
Render / UI → Bond          （需要复用的跨模块、跨层交互）
```

| 层 | 位置 | 职责 |
|---|---|---|
| GF | `Assets/FModa/Architecture/Global/GF.cs` | 单例 + `DontDestroyOnLoad`、0.1s 逻辑帧、普通 C# 模块与 Bond 的注册/获取、`Spawn/Despawn` |
| Flow | `Assets/FModa/Architecture/Flows/` | 全局只有一个 `CurrentFlow`；编排步骤、直接管理单个模块、调用 Bond 并控制流程切换；自定义方法只允许实现流程切换 |
| Bond | `Assets/FModa/Architecture/Bond/`、业务 `Bond/` | 可复用的模块间及模块与 Render / UI 间的粘合；可访问模块、渲染和 UI，不负责切换流程 |
| Module | `Assets/FModa/Modules/` | 普通 C# 类；业务规则与业务事件；实现 `IModule`；只有模块可以访问数据中心 |
| DataCenter | `Assets/FModa/Architecture/Data/` | 运行时状态 `Dictionary<long,T>`，在 `DataManager` 注册；不写业务规则。DataManager 支持按中心/条目导出及 Replace / Append 导入 |
| Render / UI | `Assets/FModa/Architecture/Renders`、`UI`、`Example/Scripts` | 表现与输入转发；不直接访问 `DataManager`；跨层组合逻辑交给 Bond |

## Quick Rules（详细版见 Skills）

- `GF.GetModule<ConcreteType>()`：业务接口不满足 `where T : class, IModule`，不能写 `GF.GetModule<IUnitManager>()`。
- Bond 用 `GF.GetBond<ConcreteBond>()` 获取，不存在时自动创建并按具体类型复用；需要显式创建时用 `GF.CreateBond<T>()`。
- **Flow 可以直接创建、获取、调用和移除单个模块**。多模块协作，以及模块与 Render / UI 的组合交互必须封装进 Bond；Flow 不直接操作视图细节。
- **流程切换代码只能在 Flow 中实现**。Bond / Module / Render / UI 只能发事件或返回结果，由 Flow 自己调用 `GF.flow.SwitchFlow` / `SwitchSceneAndFlow`。
- **模块必须是普通 C# 类，不得继承 `MonoBehaviour`，也不得挂载到场景中**。通过 `GF.GetModule / CreateModule / RegisterModule` 显式管理；GF 不再扫描或自动注册场景模块，Unity 消息和场景表现由 Render 负责。
- `IModule` 生命周期方法必须 `public`；销毁钩子是 `OnRemove()`（不是 `OnDestroy`）。订阅放在 `OnAwake`，用命名方法，并在 `OnRemove()`/`Exit` 成对退订。
- 不要缓存 `DataManager.X` 到字段：`DataManager.PreWarm()` 会整体替换实例。
- 当前 `DataCenter<T>` **没有** `OnDataAdded / OnDataRemoved / OnCleared`；不要订阅不存在的事件。
- 模块逻辑走 `OnUpdate`（默认 0.1s 逻辑帧）；Render 才允许每帧 `Update`。
- 池化对象用 `GF.Spawn / GF.Despawn`，不要直接 `Instantiate / Destroy`。
- **禁止到处新造类**：静态数据只放 `Assets/FModa/Architecture/Global/GameConfig.cs`（每个字段注释用途，经 `GF.config` 读取）；公用枚举只放 `Global/GameEnum.cs`；纯静态工具方法只放 `Global/PM.cs`。
- **PM 与模块无关**：其中只允许纯静态工具方法，不得访问或依赖 Module、DataCenter、Bond、Flow、Render、UI，也不得承担实体转换或跨层协作。
- **先复用再新建**：`Assets/FModa/Modules/` 下有内置模块（Input/Network/Hotfix/Modding/Multilingual/Debug/Pathfinding/SeedRandom），清单见 Skills README；没有现成能力才新建模块。
- **UI 必须继承 `UIBase`**（不能直接 `MonoBehaviour`），预制体放 `Assets/Resources/UI/`，通过 `GF.ui.OpenUI("PrefabName")` 打开；不要手动 `Instantiate` 或自己注册。
- **Render 获取场景对象 / 预制体走 `GF.sgo`**：场景对象挂 `Sgo`（或实现 `ISGO`）并填写唯一 `SgoId`，预制体在 `GameObjectManager` Inspector 登记；不要用 `GameObject.Find` / `FindObjectOfType`。
- **频繁生成 / 回收的对象继承 `Poolable`**，用 `GF.Spawn / GF.Despawn`。
- **需要打印时，用 `GF.Log`，不要直接用 `Debug.Log`**。

## Build & Test

Unity project — there is no CLI build script. All building and testing is done via the Unity Editor.

- Open the project with Unity 2022.3.38f1（project root: `E:\unity\ECMVC`）
- Main example scene: `Assets/Example/Scenes/SampleScene.unity`
- Run tests: **Window > General > Test Runner** in the Unity Editor
- CLI test run: `Unity -runTests -testPlatform editmode -projectPath <path>`

## Reference

- 完整模块纵切：`Assets/Example/Scripts/Modules/Unit/`
- 架构说明（部分内容已过期，以 Skills 中的漂移表为准）：`Assets/FModa/README/README.md`
- Skills 索引：`Assets/FModa/Architecture/Skills/README.md`
