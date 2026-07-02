# 添加包
- Xlua
- Newtonsoft Json
- UniTask

# ECMVC 架构

**ECMVC** = **E**ntity + **C**omponent + **MVC**

一套基于 Unity 的轻量架构，用 Entity-Component 管理游戏对象行为，用纯粹 MVC 管理 UI 系统。  
同时提供 **XLua 热更新下载管线** 和 **XLua 模组系统**，以及一套可复用的 **TCP 网络框架**。

---

## 目录结构

```
ECMVC/
├── Architecture/                    架构层（跨项目通用，勿随意修改）
│   ├── Components/
│   │   ├── ComponentBehaviour.cs    组件基类
│   │   └── CustomComponent.cs       Lua 热更新组件桥接（XLua 版）
│   ├── Entities/
│   │   ├── EntityBehaviour.cs       实体基类（组件管理 + 生命周期分发）
│   │   └── UnitEntity.cs            游戏单位基类（可定制公共字段）
│   ├── ModSystem/                   模组系统（XLua 版，取代旧 MoonSharp）
│   │   ├── ModManager.cs            XLua 模组加载器，扫描 Mods/ 目录
│   │   ├── LuaGameAPI.cs            Lua 全局 Game 对象（组件模板注册表）
│   │   ├── CustomComponent.cs       Lua Table 接入 EC 生命周期分发
│   │   ├── ModInfo.cs               Mod 元数据
│   │   └── ModSetting.cs            Mod 启用状态持久化
│   ├── MVC/                         纯 MVC UI 系统
│   │   ├── ModelBase.cs             Model 基类
│   │   ├── ViewBase.cs              View 基类
│   │   ├── ControllerBase.cs        Controller 基类
│   │   └── CounterBootstrap.cs      装配入口基类（示例）
│   └── XLua/                        热更新下载管线（独立于模组系统）
│       ├── LuaManager.cs            资源下载管理器（UniTask 版）
│       └── LuaData.cs               文件列表响应数据类
│
├── Example/                         例子层（游戏专有代码，按需参考）
│   ├── Components/
│   │   └── CHealth.cs               示例组件
│   ├── Entities/
│   ├── Bootstrap/
│   │   └── ComponentRegistry.cs     C# 组件工厂注册（指向 ModManager）
│   ├── Entities/
│   │   ├── ExampleEntity.cs         示例实体（C# 组件）
│   │   └── LuaExampleEntity.cs      示例实体（Lua 组件注入）
│   ├── Lua/
│   │   └── ExampleMod/              示例 Mod
│   │       ├── modInfo.ini
│   │       └── main.lua
│   └── MVC/
│       ├── Model/CounterModel.cs
│       ├── View/CounterView.cs
│       └── Controller/CounterController.cs
│
├── Manager/
│   └── LoadAB.cs                    AB 包加载/下载（UniTask 版）
│
├── Network/                         网络框架
│   ├── NetworkManager.cs            MonoBehaviour 管理器，主线程派发
│   ├── TCPServer.cs                 服务端，令牌认证 + 消息帧协议
│   ├── TCPClient.cs                 客户端，令牌持久化 + 自动重连
│   └── NetData.cs                   数据模型（TcpData, PlayerData）
│
└── README.md
```

---

## 一、EC（Entity-Component）

### 核心概念

| 层 | 类 | 说明 |
|---|---|---|
| 实体 | `EntityBehaviour` | 挂在 GameObject 上，管理组件、分发生命周期 |
| 单位基类 | `UnitEntity` | 继承 EntityBehaviour，预置游戏通用字段，可按需扩展 |
| 组件 | `ComponentBehaviour` | 纯 C# 类，持有行为逻辑，通过嵌套 Data 类读写实体数据 |

### 三步创建一套 EC

**Step 1 — Component**（继承 `ComponentBehaviour`，定义嵌套 Data 类，实现生命周期接口）

```csharp
public class CHealth : ComponentBehaviour, IStart, IOnDestroy
{
    [System.Serializable]
    public class Data
    {
        public int MaxHp = 100;
        public int CurrentHp;
    }

    private Data _data;

    public CHealth(UnitEntity entity, Data data) : base(entity)
    {
        _data = data;
    }

    public void Start()
    {
        _data.CurrentHp = _data.MaxHp;
    }

    public void OnDestroy() { /* 清理 */ }

    public void TakeDamage(int damage)
    {
        _data.CurrentHp -= damage;
        if (_data.CurrentHp <= 0)
            Object.Destroy(m_EgameObject);
    }
}
```

**Step 2 — Entity**（继承 `UnitEntity`，持有组件 Data 实例，Awake 中装配）

```csharp
public class ExampleEntity : UnitEntity
{
    [SerializeField] private CHealth.Data _healthData = new();

    private CHealth _cHealth;

    protected override void Awake()
    {
        _cHealth = new CHealth(this, _healthData);
        AddComponentQ(_cHealth);
        base.Awake();  // 最后调用，触发所有组件的 IAwake
    }
}
```

### 组件管理 API

```csharp
AddComponentQ(component);          // 注册组件并挂载生命周期
GetComponentQ<CHealth>();          // 获取组件（不存在返回 null）
HasComponentQ<CHealth>();          // 检查组件是否存在
RemoveComponentQ<CHealth>();       // 移除组件并卸载生命周期
```

### 生命周期接口

| 接口 | 对应 Unity 回调 |
|---|---|
| `IAwake` | Awake |
| `IStart` | Start |
| `IUpdate` | Update |
| `IFixedUpdate` | FixedUpdate |
| `ILateUpdate` | LateUpdate |
| `IOnDestroy` | OnDestroy |
| `IOnEnable` | OnEnable |
| `IOnDisable` | OnDisable |
| `IOnCollisionEnter2D` | OnCollisionEnter2D |
| `IOnCollisionStay2D` | OnCollisionStay2D |
| `IOnCollisionExit2D` | OnCollisionExit2D |
| `IOnTriggerEnter2D` | OnTriggerEnter2D |
| `IOnTriggerStay2D` | OnTriggerStay2D |
| `IOnTriggerExit2D` | OnTriggerExit2D |
| `IOnGUI` | OnGUI |
| `IOnDrawGizmos` | OnDrawGizmos |
| `IOnDrawGizmosSelected` | OnDrawGizmosSelected |

组件实现哪个接口，EntityBehaviour 就自动在对应时机调用它，无需手动注册。

### 标签系统

标签底层是 `HashSet<string>`，查询 O(1)。

`UnitEntity` 在 Inspector 中填写逗号分隔字符串（如 `Enemy,Ground`），Awake 时自动解析。

```csharp
entity.HasTag("Enemy");       // 查询
entity.AddTag("Stunned");     // 添加，返回 bool（是否为新增）
entity.RemoveTag("Stunned");  // 移除，返回 bool（是否存在）
```

若直接继承 `EntityBehaviour`（跳过 `UnitEntity`），需在 Awake 中手动解析：

```csharp
protected override void Awake()
{
    InitTags("Enemy,Ground");
    base.Awake();
}
```

### 子实体管理

```csharp
// 添加（自动设置 child.parentEntity = this）
AddChildEntity(child);

// 移除（自动清空 child.parentEntity）
RemoveChildEntity(child);

// 按场景层级刷新缓存（跳过自身）
List<EntityBehaviour> list = GetAllChildEntities();

// 读取缓存（不刷新）
List<EntityBehaviour> list = GetChildEntities();

// 判断 this 是否是 child 的直接父实体
bool yes = IsParentOf(child);
```

---

## 二、MVC（UI 系统）

### 层级职责

```
Model ──event──► View        数据变化时主动通知 View 刷新
Controller ──call──► Model   用户操作转化为 Model 方法调用
View    ✗  Controller        View 不感知 Controller
Controller ✗  View           Controller 不操作任何 UI 元素
```

### Model（继承 `ModelBase`）

拥有数据，变化时通过 `event` 通知，不引用 View 或 Controller。

```csharp
public class CounterModel : ModelBase
{
    public event Action<int> OnCountChanged;

    private int _count;
    public int Count
    {
        get => _count;
        private set { _count = value; OnCountChanged?.Invoke(_count); }
    }

    public void Increment() => Count++;
    public void Decrement() => Count--;
    public void Reset()     => Count = 0;
}
```

### View（继承 `ViewBase`）

订阅 Model 事件，刷新 UI，不处理任何 `Input`。

```csharp
public class CounterView : ViewBase
{
    [SerializeField] private TextMeshProUGUI _countText;

    private CounterModel _model;

    public void Bind(CounterModel model)
    {
        _model = model;
        BindEvents();
        Refresh(_model.Count);   // 立即同步当前值，避免首帧空白
    }

    protected override void BindEvents()   => _model.OnCountChanged += Refresh;
    protected override void UnbindEvents() => _model.OnCountChanged -= Refresh;

    private void Refresh(int count) => _countText.text = count.ToString();
}
```

`ViewBase.OnDestroy` 自动调用 `UnbindEvents()`，无需手动取消订阅。

### Controller（继承 `ControllerBase`）

处理用户输入，调用 Model 方法，UI Button 的 `OnClick` 直接绑定其公共方法。

```csharp
public class CounterController : ControllerBase
{
    private CounterModel _model;

    public void Init(CounterModel model) => _model = model;

    public void OnIncrementButton() => _model.Increment();
    public void OnDecrementButton() => _model.Decrement();
    public void OnResetButton()     => _model.Reset();
}
```

### Bootstrap（装配入口）

新建空 GameObject，挂载 Bootstrap 脚本，Inspector 中拖入 View 和 Controller。  
Bootstrap 是场景中**唯一同时持有三者引用**的地方。

```csharp
public class CounterBootstrap : MonoBehaviour
{
    [SerializeField] private CounterView       _view;
    [SerializeField] private CounterController _controller;

    private void Awake()
    {
        var model = new CounterModel();
        _view.Bind(model);
        _controller.Init(model);
    }
}
```

---

## 三、XLua 模组系统（ModSystem）

不需要重新编译 C# 即可向实体注入行为。Lua 脚本通过 `CustomComponent` 桥接，享有与 C# 组件相同的完整生命周期回调。

使用 **XLua** 作为 Lua 引擎（已移除旧 MoonSharp）。

### 核心概念

| 类 | 职责 |
|---|---|
| `ModManager` | 单例，初始化 LuaEnv、扫描 Mods/ 目录、加载 Mod、热重载 |
| `LuaGameAPI` | Lua 全局 `Game`，维护组件模板注册表 |
| `CustomComponent` | EC 组件，将 LuaTable 接入生命周期分发 |

### 场景搭建

新建空 GameObject，挂载 `ModManager`（路径 `ECMVC/Architecture/ModSystem/ModManager`），无需任何 Inspector 配置。  
Mod 文件夹位于 `Application.dataPath/Mods/`，编辑器下即 `Assets/Mods/`。

### Mod 文件夹结构

```
Mods/
└── MyMod/
    ├── modInfo.ini   （名称/版本/作者/描述）
    └── main.lua      （入口，可 require 其他 Lua 文件）
```

`modInfo.ini` 格式：

```ini
modName=我的模组
version=v1.0.0
author=作者名
description=简介
```

### 三步写一个 Lua 组件

**Step 1 — 在 Lua 中定义组件表**

```lua
-- Mods/MyMod/main.lua

local Ticker = {}

Ticker.Start = function(self)
    self.frame    = 0
    self.interval = 60
end

Ticker.Update = function(self)
    self.frame = self.frame + 1
    if self.frame >= self.interval then
        self.frame = 0
        Game.Log("60 帧到了")
    end
end

Ticker.OnDestroy = function(self)
    Game.Log("Ticker 销毁")
end
```

**Step 2 — 注册到框架**

```lua
Game.AddComponent("Ticker", Ticker)
```

**Step 3 — C# 实体应用模板**

```csharp
protected override void Awake()
{
    var cc = new CustomComponent(this);
    AddComponentQ(cc);
    base.Awake();
}

private void Start()
{
    ModManager.Instance.API.ApplyComponent("Ticker", this, cc);
}
```

或使用示例实体 `LuaExampleEntity`，在 Inspector 的 **Lua Components** 字段填写名称即可。

### Lua 全局变量

| 变量 | 类型 | 说明 |
|---|---|---|
| `Game` | `LuaGameAPI` | 组件注册、日志工具 |
| `ModsPath` | `string` | Mods 文件夹绝对路径 |
| `Path` | `string` | 当前 .lua 文件所在目录（每个文件执行前更新） |
| `Vector3` | `CS.UnityEngine.Vector3` | `Vector3(x, y, z)` 构造 |
| `Quaternion` | `CS.UnityEngine.Quaternion` | `Quaternion.Euler(x, y, z)` 等 |
| `Color` | `CS.UnityEngine.Color` | `Color(r, g, b, a)` 构造 |

### Game API

```lua
Game.AddComponent("name", table)   -- 注册组件模板
Game.RemoveComponent("name")       -- 取消注册
Game.HasComponent("name")          -- 检查是否已注册
Game.Log("msg")                    -- Debug.Log
Game.LogWarning("msg")             -- Debug.LogWarning
Game.LogError("msg")               -- Debug.LogError
```

### 热重载

```csharp
ModManager.Instance.ReloadMods();  // 销毁旧 LuaEnv，重新扫描加载所有 Mod
```

### self 状态隔离

`ApplyComponent` 为每个实体创建独立的 LuaTable 实例——函数闭包共享，`self` 上的变量各自独立，多个实体挂同一模板互不干扰。

```lua
-- entity_A 和 entity_B 各有独立的 self.frame，不会相互覆盖
Ticker.Update = function(self)
    self.frame = self.frame + 1
end
```

---

## 四、XLua 热更新下载管线

与模组系统**相互独立**的另一个系统，负责从远程服务器下载资源（version / Lua 脚本 / AssetBundle）。

### 核心类

| 类 | 命名空间 | 职责 |
|---|---|---|
| `LuaManager` | `ECMVC.XLua` | 下载管理器，生命周期、服务器通信、文件对比 |
| `LoadAB` | 全局 | AB 包加载 / 流式下载 |
| `LuaData` | `ECMVC.XLua` | 文件列表数据结构 |

### 工作流

```
1. CheckAndUpdate()
   ├─ 获取服务器 version 文件列表
   ├─ 对比本地文件 → 有更新？→ UpdateResource()
   └─ 无更新 → SetupLuaEnvironment()

2. UpdateResource()
   ├─ 下载 version 文件
   ├─ 下载 lua 脚本
   ├─ SetupLuaEnvironment()（加载 Tile.lua 等）
   └─ 下载 AB 包（按平台）

3. SetupLuaEnvironment()
   ├─ 注册 CustomLoader（从 persistentDataPath/lua/ 加载）
   └─ 执行 require('Tile')
```

所有异步方法使用 **UniTask**，替代传统协程。

### 测试窗口

菜单 `Assets/AB包/打开打包窗口` 可生成 version.json 和 Lua 文件到 StreamingAssets，然后上传到服务器即可分发给客户端。

---

## 五、TCP 网络框架

基于 `System.Net.Sockets.TcpClient/TcpListener` 的轻量网络框架，带**消息帧协议**和**令牌认证**。

### 核心类

| 类 | 命名空间 | 职责 |
|---|---|---|
| `NetworkManager` | `ECMVC.Net` | MonoBehaviour 单例，主线程派发，管理 Server/Client 生命周期 |
| `TCPServer` | `ECMVC.Net` | TCP 服务端，消息帧协议，令牌签发/验证 |
| `TCPClient` | `ECMVC.Net` | TCP 客户端，令牌持久化，自动超时重连 |
| `TcpData` | `ECMVC.Net` | 消息帧结构（dataType / dataName / dataContent） |

### 消息帧协议

所有消息使用 **4 字节长度头 + JSON 消息体** 的帧格式：

```
[4 bytes: 消息体长度][UTF-8 JSON: TcpData]
```

`TcpData` 包含：
- `dataType` — `"Data"`（普通数据）或 `"Event"`（事件）
- `dataName` — 事件名称（仅 Event 类型使用）
- `dataContent` — 内容字符串

### 令牌认证握手

```
客户端                             服务端
  │                                  │
  │── TCP 连接 ────────────────────→ │
  │←─ SendToken ──────────────────── │  服务端询问令牌
  │── ClientHello(token) ──────────→ │  客户端回复令牌（空=无）
  │←─ ConnectSussess ─────────────── │  clientId=""→复用  /  新 token→签发
  │── ClientConnect(PlayerData) ───→ │  原有握手
```

### 服务端 API

```csharp
var net = NetworkManager.Instance;

// 启动
net.StartServer(9999);
net.OnServerClientConnect += client => { };
net.OnServerClientData    += (client, data) => { };
net.OnServerClientEvent   += (client, event, data) => { };

// 发送
net.SendMessage("hello");              // 广播给所有
net.SendMessage("hello", targetClient); // 给指定
net.SendEvent("EventName", json);       // 广播事件

// 关闭
net.StopServer();
```

### 客户端 API

```csharp
var net = NetworkManager.Instance;

// 连接
net.StartClient("192.168.1.100", 9999);
net.OnClientConnect += () => { };
net.OnClientData    += (data) => { };
net.OnClientEvent   += (eventName, data) => { };

// 发送
net.SendData("hello");
net.SendEvent("EventName", json);

// 断开（令牌保留，下次连接自动验证）
net.DisconnectClient();
```

### 令牌机制

| 特性 | 说明 |
|---|---|
| 生成 | 服务端 MD5(端点+时间戳+计数器+GUID) → 32 位哈希 |
| 存储 | 客户端 `client_identities.json`，支持多服务器 |
| TTL | 24 小时，刷新或过期自动删除 |
| 服务端重启 | 内存令牌清空，旧令牌失效，自动签发新令牌 |
| 服务端关闭 | 广播 `ServerShutdown`，客户端删除对应令牌 |

---

## 六、注意事项

### EC 相关

**`base.Awake()` 必须在所有 `AddComponentQ` 之后调用**

`base.Awake()` 触发所有已注册组件的 `IAwake`，提前调用会导致后续注册的组件收不到 Awake。

```csharp
protected override void Awake()
{
    AddComponentQ(new CHealth(this, _healthData)); // 先注册
    base.Awake();                                   // 再触发
}
```

**同类型 Component 在同一 Entity 上只能存在一个**

`AddComponentQ<T>` 以 `T` 作为唯一键，重复添加打印警告并忽略。需要多个同类行为时，拆分子类。

**Data 类中存放数据，不要在 Data 中引用具体类型**

Data 是纯数据容器，不要在其中引用 MonoBehaviour 或其他游戏运行时对象。

```csharp
// ❌ Data 包含了游戏运行时引用
public class Data { public ETurretAttack useTarget; }

// ✅ 只声明纯数据
public class Data { public float moveSpeed = 5f; }
```

**`GetAllChildEntities()` 与 `AddChildEntity()` 不要混用**

前者清空缓存后重新遍历场景层级，后者直接操作缓存。混用会导致缓存不一致。

### MVC 相关

**Controller 不能操作任何 UI 元素**

一旦 Controller 访问 `view.text.text` 或 `view.slider.value`，View 与 Controller 就耦合了，事件驱动的意义消失。

**`Bind` 必须在 Bootstrap 的 `Awake` 中完成**

确保 View 在 `Start` 之前拿到 Model，避免 `_model` 为 null。

**一个 Model 可以绑定多个 View**

`event` 天然多播。多个面板显示同一数据时，每个 View 独立 `Bind` 同一 Model 实例即可。

**只展示数据、无用户输入的面板不需要 Controller**

直接在 Bootstrap 中 `view.Bind(model)`，省略 Controller 层。

### Lua/ModSystem 相关

**`ModManager` 必须早于依赖它的实体完成 `Start`**

`ModManager.Awake()` 负责初始化 LuaEnv 并加载所有 Mod。在 **Project Settings → Script Execution Order** 中将 `ModManager` 设为最早执行。

**XLua 代码生成**

需要在 Unity 中执行 `XLua → Generate Code`，确保需要从 Lua 调用的类型（`Vector3`、`Quaternion`、`Color`、`GameObject` 等）已配置到 `LuaCallCSharp` 列表中。

**Lua 组件名称与字段命名不能与生命周期方法重名**

`Awake` / `Start` / `Update` 等名称被框架保留为生命周期回调，不要在 Lua 组件的 `self` 上作为普通数据字段使用。

**同一 `CustomComponent` 可挂载多个 Lua 组件，顺序即执行顺序**

```lua
Game.AddComponent("A", compA)
Game.AddComponent("B", compB)
```

```csharp
api.ApplyComponent("A", cc); // A.Update 先于 B.Update 执行
api.ApplyComponent("B", cc);
```

### 编辑器工具

**AB 包构建工具**

菜单 `Assets/AB包/打开打包窗口`：

- 选择平台（Android / Windows / iOS）
- 编辑 Version 信息（版本号、更新说明）
- 打包时自动创建 `StreamingAssets/version/version.json` 和 `StreamingAssets/lua/` 目录
- Lua 内容用 VS Code 直接编辑 `StreamingAssets/lua/`
