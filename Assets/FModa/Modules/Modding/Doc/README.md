# 模组模块 (FModa.Modding)

基于 XLua 的轻量模组系统，走 GF 模块标准，**数据完全独立**。

## 文件结构

```
Assets/FModa/Modules/Modding/
├── Interface/
│   └── IModding.cs         接口定义 + ModInstanceInfo 运行时信息
├── Data/
│   └── ModManifest.cs      模组清单（mod.json 反序列化）
├── ModdingManager.cs       核心：IModule + IModding 实现
└── Doc/
    └── README.md           本文档

Mods/                       模组安装目录（项目根目录，非 Assets 内）
└── SampleMod/
    ├── mod.json            模组清单（必选）
    └── main.lua            入口脚本（由 mod.json 指定）
```

## 快速开始

### 1. 创建模组

在项目根目录 `Mods/` 下创建模组文件夹：

```
Mods/MyMod/
├── mod.json
└── main.lua
```

**mod.json**：
```json
{
  "id": "com.example.mymod",
  "name": "MyMod",
  "version": "1.0.0",
  "author": "你",
  "description": "模组功能描述",
  "entry": "main.lua"
}
```

**main.lua**：
```lua
local M = {}

function M.on_start(api)
    api:Log("模组启动！")
    api:DataSet("hp", 100)
end

function M.on_update(api)
    -- 每帧逻辑
end

function M.on_destroy(api)
    -- 清理
end

return M
```

### 2. 访问模组管理器

通过 GF 获取（自动创建）：

```csharp
var modding = GF.GetModule<ModdingManager>();
// 或按接口：var modding = GF.GetModule<ModdingManager>().InterfaceType;

// 查询所有模组
foreach (var mod in modding.GetAllMods())
    Debug.Log($"{mod.id} [{mod.state}]");

// 订阅事件
modding.OnModLoaded += info => Debug.Log($"加载完成: {info.name}");
modding.OnModError += (id, msg) => Debug.LogError($"{id}: {msg}");
```

## 生命周期

| 钩子 | 触发时机 | 说明 |
|------|----------|------|
| `on_start(api)` | 模组加载完成 | 初始化数据、绑定事件 |
| `on_update(api)` | 每逻辑帧 | 持续逻辑 |
| `on_destroy(api)` | 模组卸载前 | 清理资源、保存数据 |

## ModAPI 参考

所有模组通过 `api` 参数访问受控功能，**无法直接访问 GF、DataManager 或 Unity API**。

### 日志

| 方法 | 说明 |
|------|------|
| `api:Log(msg)` | 普通日志（带 `[modId]` 前缀） |
| `api:LogWarning(msg)` | 警告日志 |
| `api:LogError(msg)` | 错误日志 |

### 数据存储

每个模组有独立的数据空间，完全隔离：

| 方法 | 说明 |
|------|------|
| `api:DataSet(k, v)` | 存储数据 |
| `api:DataGet(k)` | 读取数据 |
| `api:DataRemove(k)` | 删除键 |
| `api:DataClear()` | 清空全部 |
| `api:DataHas(k)` | 检查键是否存在 |

### 模组信息

| 方法 | 说明 |
|------|------|
| `api.Info` | 读取 mod.json 清单（`.id`, `.name`, `.version` 等） |

## 设计原则

1. **符合模块标准**：ModdingManager 实现 IModule，通过 GF 注册，与网络模块、热更新模块同级
2. **数据完全独立**：每模组独立 LuaEnv + 独立 ModAPI 数据字典，互不可见
3. **最小权限**：Lua 只能通过 ModAPI 操作，不暴露 UnityEngine、GF、XLua 等
4. **即插即用**：将模组文件夹放入 `Mods/` 目录，重启或 `ReloadAll()` 即可加载
