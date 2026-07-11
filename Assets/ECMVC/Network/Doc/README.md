# ECMVC 网络模块

基于 TCP 的客户端-服务器网络模块，支持**单服务端 + 单客户端**。  
所有操作通过 `INetwork` 接口访问，不直接依赖具体实现。

---

## 架构概览

```
┌──────────────────────────────────┐
│          INetwork (接口)          │
├──────────────┬───────────────────┤
│  TCPServer   │  NetworkManager   │
│  (单例)      │  (MonoBehaviour)  │
├──────────────┴───────────────────┤
│  后台线程接收数据 → Enqueue →    │
│  主线程 Update 派发              │
└──────────────────────────────────┘
```

| 类 | 职责 |
|---|---|
| `INetwork` | 统一操作接口，外部代码通过此接口访问 |
| `NetworkManager` | MonoBehaviour 实现，管理服务端和客户端生命周期 |
| `TCPServer` | TCP 服务端，监听端口，接受客户端连接 |
| `TCPClient` | TCP 客户端，连接指定服务端，支持身份令牌 |
| `TcpData` | 数据传输模型 |

---

## 快速开始

### 1. 挂载 NetworkManager

将 `NetworkManager` 挂载到场景中任意 GameObject，或代码创建：

```csharp
var go = new GameObject("NetworkManager");
var net = go.AddComponent<NetworkManager>();
```

### 2. 启动服务端

```csharp
INetwork net = GetComponent<NetworkManager>();
net.StartServer(12345);
```

### 3. 连接客户端

```csharp
net.StartClient("127.0.0.1", 12345);
```

### 4. 发送数据

```csharp
// 客户端 → 服务端
net.SendData("Hello server!");
net.SendEvent("PlayerMove", "{x:10, y:20}");

// 服务端 → 客户端（广播）
net.SendMessage("Hello client!");
net.BroadcastEvent("GameStart", "{\"map\":\"arena\"}");
```

---

## API 参考

### 服务端

```csharp
// 生命周期
TCPServer server = net.StartServer(port);   // 启动
net.StopServer();                            // 关闭（自动断开客户端）

// 发送
net.SendMessage("Hello");                   // 广播
net.SendMessage("Hi", specificClient);      // 指定客户端
net.BroadcastEvent("EventName", "data");    // 广播事件
net.BroadcastEvent("Private", "data", c);   // 指定客户端事件

// 事件
net.OnServerOpen        += () => { };
net.OnServerClose       += () => { };
net.OnServerError       += (e) => { };
net.OnServerClientConnect    += (tcpClient) => { };
net.OnServerClientDisconnect += (tcpClient) => { };
net.OnServerClientData       += (tcpClient, data) => { };
net.OnServerClientEvent      += (tcpClient, name, data) => { };
```

### 客户端

```csharp
// 生命周期
TCPClient client = net.StartClient("127.0.0.1", 12345);  // 连接
net.DisconnectClient();                                     // 断开

// 发送
net.SendData("Hello server!");          // 消息
net.SendEvent("PlayerMove", "{x:10}");  // 事件

// 事件
net.OnClientConnect    += () => { };
net.OnClientDisconnect += () => { };
net.OnClientData       += (data) => { };
net.OnClientEvent      += (name, data) => { };
net.OnClientError      += (e) => { };
```

### 属性

```csharp
net.IP   = "192.168.1.100";   // 默认 "127.0.0.1"
net.Port = 8080;               // 默认 9999
```

> `IP` / `Port` 仅在 Inspector 中作为默认值展示，`StartServer` / `StartClient` 以传入参数为准。

### 工具

```csharp
string localIP = net.GetLocalIP();  // 本机局域网 IPv4 地址
```

---

## 连接握手（自动处理）

客户端连接时自动执行令牌握手，对外透明：

```
TCP 连接建立 → 服务端请求令牌 → 客户端回复本地令牌(或空)
  → 令牌有效 → 复用
  → 令牌无效/空 → 服务端签发新令牌 → 客户端保存
```

令牌保存在 `persistentDataPath/client_identities.json`，有效期 24 小时。

> 内部握手事件（`SendToken` / `ConnectSussess` / `ServerShutdown`）不会泄漏到 `OnClientEvent`。

---

## 线程安全

- 后台线程通过 `NetworkManager.Enqueue(Action)` 将回调排到主线程
- `Update()` 每帧从队列派发，所有事件回调均在主线程执行

## 依赖

- `Newtonsoft.Json` — 序列化/反序列化
- `UnityEngine.Events` — UnityAction 事件系统
