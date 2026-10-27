# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## ⚠️ 强制规则：先读 Skills，再阅读或修改代码

本项目的详细架构与设计规则**不在本文件**，而在：

`Assets/FModa/Architecture/Skills/`

**在阅读、设计、修改、重构或排查本项目任何代码之前，必须完整阅读对应规则：**

| 任务 | 必读文件 |
|---|---|
| 理解架构、追踪功能、判断代码归属 | `Assets/FModa/Architecture/Skills/fmoda-architecture/SKILL.md` |
| 新增/修改模块、数据中心、Flow | `Assets/FModa/Architecture/Skills/fmoda-module-design/SKILL.md` |
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
2. **顺序可控**：启动、更新、销毁由 GF / Flow / Priority 显式决定，不依赖 Unity 消息顺序。
3. **数据逻辑分离**：数据只描述状态，规则和事件在模块里。

依赖方向：

```
GF → Flow → Module → DataCenter → Data
Render / UI → Module        （视图绝不直接访问 DataCenter）
```

| 层 | 位置 | 职责 |
|---|---|---|
| GF | `Assets/FModa/Architecture/Global/GF.cs` | 单例 + `DontDestroyOnLoad`、0.1s 逻辑帧、模块注册/获取/移除、场景 `IModule` 自动注册、`Spawn/Despawn` |
| Flow | `Assets/FModa/Architecture/Flows/` | 全局只有一个 `CurrentFlow`；启动/销毁模块、编排模块顺序 |
| Module | `Assets/FModa/Modules/` | 业务规则与业务事件；实现 `IModule`；只有模块可以访问数据中心 |
| DataCenter | `Assets/FModa/Architecture/Data/` | 运行时状态 `Dictionary<long,T>`，在 `DataManager` 注册；不写业务规则 |
| Render / UI | `Assets/FModa/Architecture/Renders`、`UI`、`Example/Scripts` | 表现与输入转发；不直接访问 `DataManager` |

## Quick Rules（详细版见 Skills）

- `GF.GetModule<ConcreteType>()`：业务接口不满足 `where T : class, IModule`，不能写 `GF.GetModule<IUnitManager>()`。
- 场景中挂载的模块会被 `AutoRegisterSceneModules()` 自动注册；不要再手写 `RegisterModule`，否则生命周期跑两遍。
- `IModule` 生命周期方法必须 `public`；订阅放在 `OnAwake`，用命名方法，并在 `OnDestroy`/`Exit` 成对退订。
- 不要缓存 `DataManager.X` 到字段：`DataManager.PreWarm()` 会整体替换实例。
- 当前 `DataCenter<T>` **没有** `OnDataAdded / OnDataRemoved / OnCleared`；不要订阅不存在的事件。
- 模块逻辑走 `OnUpdate`（默认 0.1s 逻辑帧）；Render 才允许每帧 `Update`。
- 池化对象用 `GF.Spawn / GF.Despawn`，不要直接 `Instantiate / Destroy`。

## Build & Test

Unity project — there is no CLI build script. All building and testing is done via the Unity Editor.

- Open the project with Unity 2022.3.38f1（project root: `E:\unity\ECMVC`）
- Main example scene: `Assets/Example/Scenes/SampleScene.unity`
- Run tests: **Window > General > Test Runner** in the Unity Editor
- CLI test run: `Unity -runTests -testPlatform editmode -projectPath <path>`

## Reference

- 完整模块纵切：`Assets/Example/Scripts/Modules/Unit/`
- 架构说明（部分内容已过期，以 Skills 中的漂移表为准）：`Assets/Example/README/README.md`
- Skills 索引：`Assets/FModa/Architecture/Skills/README.md`
