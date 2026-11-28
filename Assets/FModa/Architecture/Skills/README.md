# FModa Skills

面向后续 AI / 新人的项目内 skill 包。目标是让接手者**先读懂架构，再按规则设计模块**，避免在视图层、数据中心和流程之间随意堆代码。

## 技能列表

| Skill | 何时使用 | 核心内容 |
|---|---|---|
| [`fmoda-architecture`](fmoda-architecture/SKILL.md) | 第一次接触仓库、要判断代码归属、追踪功能链路、修改前确认现状 | 分层与依赖方向、Bond / Flow / PM 职责边界、概念→文件地图、推荐阅读顺序、文档漂移表、动手前检查、反模式信号 |
| [`fmoda-module-design`](fmoda-module-design/SKILL.md) | 新增/修改模块、数据中心、模块接口、Bond、Flow | 六步实现流程、目录与命名规范、Data/DataCenter/Manager 代码骨架、Bond 与 Flow 示例、注册路径对照、生命周期陷阱、自检清单 |
| [`fmoda-view-ui`](fmoda-view-ui/SKILL.md) | 编写渲染器、UIBase 面板、对象池对象 | **UI 必须继承 `UIBase` 并走 `GF.ui.OpenUI`（不能直接 `MonoBehaviour`）**、Render 通过 `GF.sgo`/`Sgo` 获取场景对象与预制体、跨层组合交给 Bond、`OnBind/OnUnbind/OnRefresh` 生命周期、`Poolable` 与 `Spawn/Despawn` |

## 按层找方法

| 要创建 / 修改的层 | 看哪里 |
|---|---|
| 数据层（`IData` + `DataCenter<T>`） | `fmoda-module-design` 第 1、2 步；注册在 `DataManager`（第 5 步） |
| 模块层（接口 + Manager + 生命周期） | `fmoda-module-design` 第 3、4 步 |
| Bond（模块间、模块与 Render / UI 间的可复用粘合） | `fmoda-module-design` 第 6 步；通过 `GF.GetBond<T>()` 获取 |
| Flow（步骤编排、单模块管理、切换流程） | `fmoda-module-design` 第 6 步；`IFlow.Enter(IFlow lastFlow, List<object> list)` |
| Render（场景表现、输入轮询） | `fmoda-view-ui` 第 1 节；场景对象/预制体走 `GF.sgo` |
| UI（面板） | `fmoda-view-ui` 第 2 节；**必须继承 `UIBase`**，走 `GF.ui.OpenUI` |
| 对象池 | `fmoda-view-ui` 第 4 节；`Poolable` + `GF.Spawn / GF.Despawn` |

## 全局硬性约定（优先级高于其他规则）

这三条是为了防止「到处新造类」。新建任何类型之前，先对照下表：

| 内容 | 唯一位置 | 规则 |
|---|---|---|
| 静态数据（全局配置、常量、只读表、默认值） | `Assets/FModa/Architecture/Global/GameConfig.cs` | 只在这里加字段；每个字段必须用注释 / `[Tooltip]` 写明「这是什么、谁在用、单位或取值范围」。运行时通过 `GF.config.xxx` 读取。不要再新建配置类、常量类或 `public static` 数据类。 |
| 纯静态工具方法（与模块无关的计算、格式化、无状态算法） | `Assets/FModa/Architecture/Global/PM.cs` | 统一写成 `PM.xxx(...)`。不得访问或依赖 Module、DataCenter、Bond、Flow、Render、UI；实体转换与跨层协作放 Bond。不要再新建 `Utils` / `Helper` / `Common` / `Extensions`。 |
| 公用枚举（两个及以上模块或视图会用的枚举） | `Assets/FModa/Architecture/Global/GameEnum.cs` | 放进 `FModa.Enum` 命名空间。不要在各自模块的 Data 里重复定义同一语义的枚举，再写转换适配器。 |

补充：

- 判定标准是「是否被多个类 / 模块共享」。只在单个类内部使用、不对外暴露的 `private const` / `private static` 可以留在类内；一旦第二个类也要用，就按上表上移。
- **跨模块或跨层共享不等于放进 PM**：只要方法与模块、数据中心、实体或视图有关，就应归入对应模块或 Bond；PM 只接受纯工具函数。
- 本条约束的是「业务静态数据 / 常量 / 只读表」；`GF.Instance`、`DataManager.X` 这类框架管理器实例已有明确归属，不在此列。
- `GameConfig` 是 `ScriptableObject`，实例在 `Assets/Resources/GameConfig.asset`；新增字段后要在 Inspector 里也能看懂用途。
- 对框架、内置模块、业务模块、`Assets/Example` 同样生效。已有代码尚未迁移的，改到哪就顺手迁到哪，不要新增第二套。

## 内置模块：先查再写

`Assets/FModa/Modules/` 下已有可直接使用的模块。**实现任何功能前，先在这里找一遍**；已有能力就调用或扩展，不要重复造轮子。

| 目录 | 形态 | 能解决什么 | 入口 |
|---|---|---|---|
| `Modules/Input/` | `IModule` | 统一输入：键鼠 / 手柄 / 触摸，轮询 + 事件，自定义按键 | `IInputManager`（`GF.GetModule<InputManager>()`） |
| `Modules/Network/` | `IModule` | TCP 联机：单服务端 + 单客户端 | `INetwork` |
| `Modules/Hotfix/` | `IModule` | XLua 热更新、AB 加载 | `IHotfix` |
| `Modules/Modding/` | `IModule` | XLua 模组加载 / 管理 | `IModding` |
| `Modules/Multilingual/` | `IModule` | 多语言：自动跟随系统语言、运行时添加语言包 | `IMultilingualManager` |
| `Modules/Debug/` | 普通组件 | 游戏内日志面板 | `GameLogger`（挂在场景对象上，不是 `IModule`） |
| `Modules/Pathfinding/` | 第三方库 | 2D NavMesh 寻路（NavMeshPlus） | `NavMeshComponents` |
| `Modules/SeedRandom/` | `IModule` | 设置 / 清除种子、可复现序列、独立种子随机，区间左闭右开 | `ISeedRandomManager`（`GF.GetModule<SeedRandomManager>()`） |
| `Modules/IModule.cs` | 契约 | 模块标记接口 | — |

- 每个模块的具体用法看它自己的 `Doc/README.md` 和 `Interface/`。
- `Assets/Example/Scripts/Modules/Unit/` 是教学示例，不是内置服务；可以照结构抄，不要把它当生产模块依赖。
- 确实没有现成能力时再新建模块，并按 `fmoda-module-design` 的六步落地。

## 使用顺序

0. 任何任务 → 先看本文件的「全局硬性约定」和「内置模块」。
1. 不熟悉项目 → 先读 `fmoda-architecture`。
2. 要加功能 → 读 `fmoda-module-design`，按六步落地。
3. 要写表现层 → 读 `fmoda-view-ui`。
4. 任何改动前 → 回到 `fmoda-architecture` 第 6 节做硬性检查。

## 说明

- 这三份 skill 是**项目文档**，放在 `Assets/` 下便于随代码一起版本管理。
- 已配置入口 skill：`.claude/skills/fmoda-rules/SKILL.md`。它会在任何代码阅读/修改任务开始前要求先读本目录下的详细规则。
- 根目录 `CLAUDE.md` 也写入了同样的强制规则，作为每个会话的默认提醒。
- 文档中的路径、成员名基于当前代码快照。**代码与文档冲突时以代码为准**，并在同一次改动中修正文档。
