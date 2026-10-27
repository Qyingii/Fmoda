# FModa Skills

面向后续 AI / 新人的项目内 skill 包。目标是让接手者**先读懂架构，再按规则设计模块**，避免在视图层、数据中心和流程之间随意堆代码。

## 技能列表

| Skill | 何时使用 | 核心内容 |
|---|---|---|
| [`fmoda-architecture`](fmoda-architecture/SKILL.md) | 第一次接触仓库、要判断代码归属、追踪功能链路、修改前确认现状 | 分层与依赖方向、概念→文件地图、推荐阅读顺序、文档漂移表、动手前检查、反模式信号 |
| [`fmoda-module-design`](fmoda-module-design/SKILL.md) | 新增/修改模块、数据中心、模块接口、Flow 接入 | 六步实现流程、目录与命名规范、Data/DataCenter/Manager 代码骨架、注册路径对照、生命周期陷阱、自检清单 |
| [`fmoda-view-ui`](fmoda-view-ui/SKILL.md) | 编写渲染器、UIBase 面板、对象池对象 | Render/UI 的职责边界、`OnBind/OnUnbind/OnRefresh` 生命周期、`UIManager` 打开/关闭/子 UI、`Poolable` 与 `Spawn/Despawn`、常见错误 |

## 使用顺序

1. 不熟悉项目 → 先读 `fmoda-architecture`。
2. 要加功能 → 读 `fmoda-module-design`，按六步落地。
3. 要写表现层 → 读 `fmoda-view-ui`。
4. 任何改动前 → 回到 `fmoda-architecture` 第 6 节做硬性检查。

## 说明

- 这三份 skill 是**项目文档**，放在 `Assets/` 下便于随代码一起版本管理。
- 已配置入口 skill：`.claude/skills/fmoda-rules/SKILL.md`。它会在任何代码阅读/修改任务开始前要求先读本目录下的详细规则。
- 根目录 `CLAUDE.md` 也写入了同样的强制规则，作为每个会话的默认提醒。
- 文档中的路径、成员名基于当前代码快照。**代码与文档冲突时以代码为准**，并在同一次改动中修正文档。
