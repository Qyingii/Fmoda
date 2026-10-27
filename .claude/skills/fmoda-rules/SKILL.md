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
2. 这份状态的唯一写入者是谁？变化如何广播给视图？
3. 谁创建、初始化、销毁它？场景自动注册还是 Flow 管理？
4. 视图是否绕过了模块，直接访问 `DataManager`？

任何一条答不上来，继续读，不要堆代码。

## 硬性禁止

- 视图（Render / UI）直接访问 `DataManager`
- 模块直接引用其他模块的具体实现
- 场景模块已经自动注册，又手写 `RegisterModule`
- 把 `IModule` 生命周期方法写成 private
- 在 Unity `Awake` 里订阅模块事件
- 把 `DataManager.X` 缓存到字段
- 订阅 `DataCenter<T>` 当前并不存在的 `OnDataAdded / OnDataRemoved / OnCleared`

详细说明、代码骨架、自检清单见上述三份 `SKILL.md`。本文件只做入口，不复制规则；**Assets 下的 Skills 是唯一源**。
