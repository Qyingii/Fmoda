using System;
using System.Net.Sockets;
using UnityEngine.Events;

namespace ECMVC.Net
{
    /// <summary>
    /// 网络管理器接口 —— 定义 TCP 服务器/客户端的生命周期、事件与方法契约。
    ///
    /// 实现类 <see cref="NetworkManager"/> 以单例形式挂载在场景中，
    /// 外部代码应通过此接口访问网络功能，而非直接依赖具体实现。
    /// </summary>
    public interface INetwork
    {
        // ── 属性 ──

        /// <summary> TCP 服务器实例（null 表示未启动）</summary>
        TCPServer Server { get; }

        /// <summary> TCP 客户端实例（null 表示未连接）</summary>
        TCPClient Client { get; }

        /// <summary> 当前是否为服务器模式</summary>
        bool IsServer { get; }

        /// <summary> 当前是否为客户端模式</summary>
        bool IsClient { get; }

        /// <summary> 服务器 IP 地址（默认 127.0.0.1）</summary>
        string IP { get; set; }

        /// <summary> 端口号（默认 9999）</summary>
        int Port { get; set; }

        // ── 服务端事件 ──

        /// <summary> 有客户端连接时触发</summary>
        event UnityAction<TcpClient> OnServerClientConnect;

        /// <summary> 有客户端断开时触发</summary>
        event UnityAction<TcpClient> OnServerClientDisconnect;

        /// <summary> 收到客户端数据时触发</summary>
        event UnityAction<TcpClient, string> OnServerClientData;

        /// <summary> 收到客户端事件时触发（含事件名与数据）</summary>
        event UnityAction<TcpClient, string, string> OnServerClientEvent;

        /// <summary> 客户端连接出错时触发</summary>
        event UnityAction<TcpClient, Exception> OnServerClientError;

        /// <summary> 服务器成功开启时触发</summary>
        event UnityAction OnServerOpen;

        /// <summary> 服务器关闭时触发</summary>
        event UnityAction OnServerClose;

        /// <summary> 服务器发生错误时触发</summary>
        event UnityAction<Exception> OnServerError;

        // ── 客户端事件 ──

        /// <summary> 成功连接到服务器时触发</summary>
        event UnityAction OnClientConnect;

        /// <summary> 与服务器断开连接时触发</summary>
        event UnityAction OnClientDisconnect;

        /// <summary> 收到服务器数据时触发</summary>
        event UnityAction<string> OnClientData;

        /// <summary> 收到服务器事件时触发（含事件名与数据）</summary>
        event UnityAction<string, string> OnClientEvent;

        /// <summary> 客户端连接或通信出错时触发</summary>
        event UnityAction<Exception> OnClientError;

        // ── 服务端 API ──

        /// <summary> 启动 TCP 服务器，监听指定端口</summary>
        /// <param name="port">监听端口</param>
        /// <returns>服务器实例</returns>
        TCPServer StartServer(int port);

        /// <summary> 关闭服务器，断开所有客户端</summary>
        void StopServer();

        /// <summary> 向客户端发送消息（client 为 null 时广播给所有客户端）</summary>
        /// <param name="message">消息内容</param>
        /// <param name="client">目标客户端，null 表示广播</param>
        void SendMessage(string message, TcpClient client = null);

        /// <summary> 向客户端发送事件（client 为 null 时广播给所有客户端）</summary>
        /// <param name="eventName">事件名称</param>
        /// <param name="message">事件数据</param>
        /// <param name="client">目标客户端，null 表示广播</param>
        void SendEvent(string eventName, string message, TcpClient client = null);

        // ── 客户端 API ──

        /// <summary> 连接到指定服务器</summary>
        /// <param name="ip">服务器 IP</param>
        /// <param name="port">服务器端口</param>
        /// <returns>客户端实例</returns>
        TCPClient StartClient(string ip, int port);

        /// <summary> 断开与服务器的连接</summary>
        void DisconnectClient();

        /// <summary> 向服务器发送消息</summary>
        /// <param name="message">消息内容</param>
        void SendData(string message);

        /// <summary> 向服务器发送事件</summary>
        /// <param name="eventName">事件名称</param>
        /// <param name="message">事件数据</param>
        void SendEvent(string eventName, string message);

        // ── 工具 ──

        /// <summary> 获取本机局域网 IPv4 地址</summary>
        /// <returns>192.168.x.x 或 10.x.x.x，失败返回 127.0.0.1</returns>
        string GetLocalIP();
    }
}
