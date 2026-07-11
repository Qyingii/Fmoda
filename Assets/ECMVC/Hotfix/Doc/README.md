# ECMVC 热更新系统使用文档

## 目录结构

```
Assets/ECMVC/Hotfix/
├── Doc/
│   └── README.md                  ← 本文件
├── Editor/
│   └── CreateAB.cs                ← AB 包打包工具 + version.json 生成
├── Interface/
│   └── IHotfix.cs                 ← 热更新接口（供外部调用）
├── Test/
│   ├── Editor/
│   │   └── HotfixTestCfg.cs       ← 热更新类型配置（推荐方式）
│   ├── lua/
│   │   └── main.lua               ← 文件加载版补丁示例
│   ├── HotfixDemo.cs              ← 演示用 C# 类
│   ├── HotfixDemoTest.cs          ← 热更功能演示（理解 hotfix 机制）
│   └── HotfixIntegrationTest.cs   ← 全流程集成测试
├── HotfixData.cs                  ← 数据模型（FileData / VersionData）
├── HotfixManager.cs               ← 热更新管理器单例（入口）
├── ResourcesDownload.cs           ← 资源下载器
├── LuaLoad.cs                     ← Lua 环境管理器
└── LoadAB.cs                      ← AB 包加载 / 下载
```

---

## 一、架构概览

```
┌─────────────────────────────────────────────────────────┐
│                    外部代码 / UI                         │
│   IHotfix.State / .RemoteVersion / .事件                 │
└──────────────┬──────────────────────────────────────────┘
               │ 通过 IHotfix 接口交互
┌──────────────▼──────────────────────────────────────────┐
│                   HotfixManager                          │
│   （单例，管理编排流程）                                  │
│   CheckAndUpdateAsync → 版本检查 → 按需下载 → 加载 Lua   │
└──────┬──────────────────────┬───────────────────────────┘
       │                      │
┌──────▼──────────┐  ┌───────▼───────────┐
│ ResourcesDownload│  │     LuaLoad       │
│ 服务器通信        │  │ LuaEnv 生命周期   │
│ 文件对比下载      │  │ main.lua 加载     │
│ version.json 解析 │  │ AB 包加载         │
└─────────────────┘  └───────────────────┘
```

### 版本驱动流程

```
Start / StartCheckUpdate()
    │
    ▼
GetRemoteVersionAsync() ─── 失败 ──→ 加载本地 Lua，结束
    │
    ▼
比对主版本号 (remote.version vs local.version)
    ├── 不同 → 打印更新提示 + 下载链接
    │          保存远程版本到本地
    │          ↓（继续检查热更）
    └── 相同 → 直接进入热更检查
    │
    ▼
比对热更版本号 (remote.hotfixVersion vs local.hotfixVersion)
    ├── 不同 → 下载 lua + AB 资源
    └── 相同 → 跳过，使用本地资源
    │
    ▼
加载 Lua 环境 → Complete
```

---

## 二、环境配置（一次性的准备工作）

### 2.1 下载热更新工具

从 [xLua GitHub Releases](https://github.com/Tencent/xLua/releases) 下载，将 `Tools/` 文件夹复制到**项目根目录**：

```
E:\unity\ECMVC/
├── Assets/
├── Tools/                    ← 需要补全
│   └── XLuaHotfixInject.exe
└── ...
```

### 2.2 添加宏定义

`File → Build Settings → Player Settings → Scripting Define Symbols` 添加：

```
HOTFIX_ENABLE
```

### 2.3 生成代码 & 注入

| 步骤 | 菜单 | 说明 |
|------|------|------|
| 1 | `XLua → Generate Code` | 生成 LuaCallCSharp / CSharpCallLua 桥接代码 |
| 2 | `XLua → Hotfix Inject In Editor` | 注入 hotfix 桩到 C# 程序集 |

> 每次修改了被 `[Hotfix]` 标记的类后，都需要重新执行第 2 步。
> 第 2 步成功后控制台会打印 `hotfix inject finish!`

### 2.4 注册热更新类型

打开 `Assets/ECMVC/Hotfix/Test/Editor/HotfixTestCfg.cs`，在列表中追加需要热更的类型：

```csharp
[Hotfix]
public static List<Type> hotfixTypes = new List<Type>
{
    typeof(ECMVC.Hotfix.Test.HotfixDemo),
    typeof(你的其他类),
};
```

---

## 三、部署热更新资源

### 3.1 生成 version.json

在 Unity 编辑器菜单 `Assets → AB包 → 打开打包窗口`，填写：

| 字段 | 说明 |
|------|------|
| 版本号 | App 主版本号（如 `v1.0.1`） |
| 新版本下载地址 | 完整 APK/IPA 下载链接 |
| 更新说明 | 主版本更新内容 |
| **热更新版本号** | 热更资源版本（如 `v1.0.1-hotfix1`） |
| **热更新说明** | 本次热更内容 |

点击 **仅生成 Version 文件** 或 **开始打包**，生成的 `version.json` 位于 `StreamingAssets/version/version.json`。

### 3.2 编写 Lua 补丁

编辑 `StreamingAssets/lua/main.lua`（部署到服务器后自动分发）：

```lua
-- 示例：替换 C# 方法
xlua.hotfix(CS.YourNamespace.YourClass, 'MethodName', function(self, param)
    -- 新的逻辑
end)

-- 批量替换
xlua.hotfix(CS.YourNamespace.YourClass, {
    Method1 = function(self) ... end;
    Method2 = function(self, v) ... end;
})
```

> **属性热更注意**：必须访问 backing field，不能访问属性自身，否则递归溢出。
> ```lua
> -- ✓ 正确
> get_Title = function(self) return self._title end
> -- ✗ 错误（死循环）
> get_Title = function(self) return self.Title end
> ```

### 3.3 部署到服务器

将 `StreamingAssets/` 下的 `version/`、`lua/`、`AssetBundles_*/` 文件夹上传到热更新服务器的 `public/` 目录。

### 3.4 启动服务器

```bash
cd D:\练习\Sever\hotfix-server
node server.js
```

服务启动后自动释放被占用的端口。

---

## 四、客户端集成

### 4.1 场景设置

1. 在场景中新建一个 GameObject，挂载 `HotfixManager` 脚本
2. 在 Inspector 中配置服务器 URL：
   - **Url**: 正式服务器地址（`https://frp-gap.com:64849`）
   - **Debug Url**: 本地调试地址（`https://localhost:6567`）
   - 编辑器环境下自动使用 Debug Url

### 4.2 通过 IHotfix 接口交互

```csharp
using ECMVC.Hotfix;

// 获取管理器（单例）
IHotfix hotfix = HotfixManager.Instance;

// ── 检查属性 ──
UpdateState state = hotfix.State;         // 当前状态
bool hasUpdate = hotfix.HasNewHotfix;      // 是否有热更新
string dlUrl = hotfix.AppDownloadUrl;      // 新版本下载地址
VersionData remote = hotfix.RemoteVersion; // 远程版本信息

// ── 订阅事件 ──
hotfix.OnVersionChecked += (remote, local) =>
{
    Debug.Log($"远程版本: {remote.version}, 本地: {local?.version}");
};

hotfix.OnStateChanged += state =>
{
    loadingUI.SetActive(state == UpdateState.Downloading);
};

hotfix.OnUpdateError += msg =>
{
    errorText.text = $"更新失败: {msg}";
};

hotfix.OnLuaEnvironmentReady += () =>
{
    Debug.Log("Lua 环境就绪，可以开始游戏逻辑");
};

// ── 手动触发 ──
hotfix.StartCheckUpdate();     // 检查更新（异步，自动下载）
hotfix.StartFullUpdate();      // 强制完整更新
```

### 4.3 状态机

```csharp
public enum UpdateState
{
    Idle,        // 空闲
    Checking,    // 正在检查远程版本
    Downloading, // 正在下载资源
    LoadingLua,  // 正在加载 Lua
    Complete,    // 更新完成
    Error,       // 出错
}
```

### 4.4 完整示例

参考 `Assets/ECMVC/Hotfix/Test/HotfixIntegrationTest.cs`：

```csharp
public class MyGameLauncher : MonoBehaviour
{
    async UniTask Start()
    {
        while (HotfixManager.Instance == null)
            await UniTask.Yield();

        IHotfix hotfix = HotfixManager.Instance;
        await hotfix.UpdateResourceAsync();

        // 此时热更已完成，开始游戏
        EnterGame();
    }
}
```

---

## 五、服务端 API 说明

热更新服务器基于 Express，在 `D:\练习\Sever\hotfix-server\`。

| 接口 | 方法 | 说明 |
|------|------|------|
| `/api/files?childPath={path}` | GET | 获取指定路径的文件列表（含 MD5） |
| `/{childPath}/{fileName}` | GET | 静态文件下载 |

`childPath` 可选值：`version`、`lua`、`AssetBundles_Android`、`AssetBundles_Windows`、`AssetBundles_IOS`。

---

## 六、常见问题

### Q: `xlua.access, no field __Hotfix0_Add`

未执行 Hotfix Inject。执行 `XLua → Hotfix Inject In Editor`。

### Q: 属性热更导致堆栈溢出

```lua
-- 错误：self.Title 再次触发 hotfix 钩子
set_Title = function(self, v)
    self.Title = v   -- 死循环
end

-- 正确：访问 backing field
set_Title = function(self, v)
    self._title = v  -- ✓
end
```

### Q: C stack overflow

同上，属性 getter/setter 中必须访问 backing field，不能访问属性本身。

### Q: `Collection was modified`

多个请求并发操作文件列表。已内置 `_isUpdating` 防重入锁，串联调用会自动跳过。

### Q: `dispose a LuaEnv with C# callback`

场景卸载时 hotfix 桥接尚有残留。已在 `LuaLoad.Dispose()` 中 catch 处理，不影响内存回收。

### Q: 测试时看不到热更效果

1. 检查 `HOTFIX_ENABLE` 宏是否已添加
2. 检查是否执行了 `Generate Code` + `Hotfix Inject In Editor`
3. 检查服务器是否运行，URL 是否正确
4. 检查 `version.json` 中的 `hotfixVersion` 是否与本地不同
