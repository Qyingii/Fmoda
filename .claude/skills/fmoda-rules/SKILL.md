---
name: fmoda-rules
description: FModa/ECMVC 项目的强制规则入口。在阅读、设计、修改、重构或排查本项目任何代码之前使用。它要求先完整阅读 Assets/FModa/Architecture/Skills 下的架构、模块设计和视图层规则，再开始工作。
---

# FModa 强制规则入口

本项目所有详细设计规则都在：

`Assets/FModa/Architecture/Skills/`

## 开始任何代码工作之前

按任务**完整阅读**（不是搜索片段）：

| 任务 | 必读文件 |
|---|---|
| 理解架构、追踪功能链路、判断代码归属 | `Assets/FModa/Architecture/Skills/fmoda-architecture/SKILL.md` |
| 新增/修改模块、数据中心、Flow | `Assets/FModa/Architecture/Skills/fmoda-module-design/SKILL.md` |
| 编写 Render、UI、对象池 | `Assets/FModa/Architecture/Skills/fmoda-view-ui/SKILL.md` |

跨领域任务必须全部阅读。**未读完对应规则，不得开始设计或写代码。**

## 读完后的自检

动手前必须能回答：

1. 改动属于 Data / DataCenter / Module / Flow / Render / UI 哪一层？
2. `Assets/FModa/Modules/` 里有没有现成模块能直接解决？（先查再写）
3. 这份状态的唯一写入者是谁？变化如何广播给视图？
4. 谁创建、初始化、销毁它？场景自动注册还是 Flow 管理？
5. 视图是否绕过了模块，直接访问 `DataManager`？
6. 视图入口是否正确：UI 继承 `UIBase` 并走 `GF.ui.OpenUI`；场景对象 / 预制体走 `GF.sgo`？

任何一条答不上来，继续读，不要堆代码。

## 硬性禁止

- 视图（Render / UI）直接访问 `DataManager`
- 模块直接引用其他模块的具体实现
- 场景模块已经自动注册，又手写 `RegisterModule`
- 把 `IModule` 生命周期方法写成 private
- 把 `IModule` 的销毁钩子写成 `OnDestroy`（接口方法是 `OnRemove`）
- 在 Unity `Awake` 里订阅模块事件
- 把 `DataManager.X` 缓存到字段
- 订阅 `DataCenter<T>` 当前并不存在的 `OnDataAdded / OnDataRemoved / OnCleared`
- 新建静态数据类 / 常量类 / `ScriptableObject` 配置类（静态数据只放 `Global/GameConfig.cs`，并注释用途）
- 新建 `Utils` / `Helper` / `Common` / `Extensions` 等静态工具类（公用静态方法只放 `Global/PM.cs`）
- 在模块里重复定义跨模块枚举（公用枚举只放 `Global/GameEnum.cs`）
- 没检查 `Assets/FModa/Modules` 就新建同职责模块（重复造轮子）
- UI 面板直接继承 `MonoBehaviour`（必须继承 `UIBase`，预制体放 `Resources/UI`，通过 `GF.ui.OpenUI` 打开）
- 手动 `Instantiate` UI、手动 `Register` 或 `AddComponent<UIBase>` 绕过 `UIManager`
- 用 `GameObject.Find` / `FindObjectOfType` 找场景对象（用 `Sgo`/`ISGO` + `GF.sgo`）

详细说明、代码骨架、自检清单见上述三份 `SKILL.md`。本文件只做入口，不复制规则；**Assets 下的 Skills 是唯一源**。
