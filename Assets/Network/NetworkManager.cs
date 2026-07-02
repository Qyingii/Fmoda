using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Events;

namespace ECMVC.Net
{
    /// <summary>
    /// 网络管理器 —— 框架层，管理 TCPServer / TCPClient 生命周期，
    /// 提供主线程派发和干净的事件接口。
    ///
    /// 使用方式：
    ///   场景中挂载此脚本，通过 Inspector 配置 IP / Port。
    ///   或代码中直接设置 NetworkManager.Instance.IP / Port。
    /// </summary>
    public class NetworkManager : MonoBehaviour
    {
        public static NetworkManager Instance;

        // ── 主线程派发队列 ─────────────────────────────────────────────────

        private static readonly Queue<Action> _mainThreadActions = new Queue<Action>();

        // ── TCP 实例 ───────────────────────────────────────────────────────

        private TCPServer _server;
        private TCPClient _client;
        private bool _isServer;
        private bool _isClient;

        [SerializeField] private string _ip   = "127.0.0.1";
        [SerializeField] private int    _port = 9999;

        // ── 公开属性 ───────────────────────────────────────────────────────

        public TCPServer Server       => _server;
        public TCPClient Client       => _client;
        public bool      IsServer     => _isServer;
        public bool      IsClient     => _isClient;
        public string    IP           { get => _ip;   set => _ip   = value; }
        public int       Port         { get => _port; set => _port = value; }

        // ── 服务端事件（主线程回调） ───────────────────────────────────────

        public UnityAction<TcpClient>             OnServerClientConnect;
        public UnityAction<TcpClient>             OnServerClientDisconnect;
        public UnityAction<TcpClient, string>     OnServerClientData;
        public UnityAction<TcpClient, string, string> OnServerClientEvent;
        public UnityAction<TcpClient, Exception>  OnServerClientError;
        public UnityAction                       OnServerOpen;
        public UnityAction                       OnServerClose;
        public UnityAction<Exception>             OnServerError;

        // ── 客户端事件（主线程回调） ───────────────────────────────────────

        public UnityAction                       OnClientConnect;
        public UnityAction                       OnClientDisconnect;
        public UnityAction<string>               OnClientData;
        public UnityAction<string, string>       OnClientEvent;
        public UnityAction<Exception>             OnClientError;

        // ══════════════════════════════════════════════════════════════════
        //  Unity 生命周期
        // ══════════════════════════════════════════════════════════════════

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            lock (_mainThreadActions)
            {
                while (_mainThreadActions.Count > 0)
                    _mainThreadActions.Dequeue().Invoke();
            }
        }

        private void OnApplicationQuit()
        {
            StopServer();
            DisconnectClient();
        }

        // ── 主线程派发 ─────────────────────────────────────────────────────

        /// <summary> 将回调排到主线程执行（供后台线程调用）</summary>
        public static void Enqueue(Action action)
        {
            lock (_mainThreadActions)
                _mainThreadActions.Enqueue(action);
        }

        // ══════════════════════════════════════════════════════════════════
        //  服务端 API
        // ══════════════════════════════════════════════════════════════════

        /// <summary> 开启 TCP 服务器 </summary>
        public TCPServer StartServer(int port)
        {
            if (_server != null)
            {
                Debug.Log("[Network] 服务器已开启");
                return _server;
            }

            _server = new TCPServer();

            _server.OnOpen += () =>
            {
                _port     = port;
                _isServer = true;
                Debug.Log($"[Network] 服务器已开启，端口: {port}");
                Enqueue(() => OnServerOpen?.Invoke());
            };

            _server.OnClose += () =>
            {
                _isServer = false;
                Enqueue(() => OnServerClose?.Invoke());
            };

            _server.OnError += (error) =>
            {
                Enqueue(() => OnServerError?.Invoke(error));
            };

            _server.OnClientOpen += (client) =>
            {
                Enqueue(() => OnServerClientConnect?.Invoke(client));
            };

            _server.OnClientClose += (client) =>
            {
                Enqueue(() => OnServerClientDisconnect?.Invoke(client));
            };

            _server.OnClientData += (client, data) =>
            {
                Enqueue(() => OnServerClientData?.Invoke(client, data));
            };

            _server.OnClientEvent += (client, eventName, data) =>
            {
                Enqueue(() => OnServerClientEvent?.Invoke(client, eventName, data));
            };

            _server.OnClientError += (client, error) =>
            {
                Enqueue(() => OnServerClientError?.Invoke(client, error));
            };

            _server.StartServer(port);
            return _server;
        }

        /// <summary> 关闭服务器 </summary>
        public void StopServer()
        {
            if (_server == null) return;

            // 断开前清理事件，避免残留
            OnServerClientConnect    = null;
            OnServerClientDisconnect = null;
            OnServerClientData       = null;
            OnServerClientEvent      = null;
            OnServerClientError      = null;

            _server.StopServer();
            _server   = null;
            _isServer = false;
            Debug.Log("[Network] 服务器已关闭");
        }

        /// <summary> 向客户端发送消息，client 为 null 时广播给所有客户端 </summary>
        public void SendMessage(string message, System.Net.Sockets.TcpClient client = null)
        {
            if (_server == null)
            {
                Debug.Log("[Network] 不是服务端，不能发送消息");
                return;
            }

            var data = JsonConvert.SerializeObject(new TcpData()
            {
                dataType = "Data",
                dataContent = message,
            });

            if (client == null)
                foreach (var c in _server.Clients)
                    _server.SendMessage(data, c);
            else
                _server.SendMessage(data, client);
        }

        /// <summary> 向客户端发送事件，client 为 null 时广播给所有客户端 </summary>
        public void SendEvent(string eventName, string message, System.Net.Sockets.TcpClient client = null)
        {
            if (_server == null)
            {
                Debug.Log("[Network] 不是服务端，不能发送事件");
                return;
            }

            var data = JsonConvert.SerializeObject(new TcpData()
            {
                dataType    = "Event",
                dataName    = eventName,
                dataContent = message,
            });

            if (client == null)
                foreach (var c in _server.Clients)
                    _server.SendMessage(data, c);
            else
                _server.SendMessage(data, client);
        }

        // ══════════════════════════════════════════════════════════════════
        //  客户端 API
        // ══════════════════════════════════════════════════════════════════

        /// <summary> 连接到服务器 </summary>
        public TCPClient StartClient(string ip, int port)
        {
            if (_client != null)
            {
                Debug.Log("[Network] 客户端已连接");
                return _client;
            }

            _ip   = ip;
            _port = port;
            _client = new TCPClient();

            _client.OnOpen += () =>
            {
                _isClient = true;
                Enqueue(() => OnClientConnect?.Invoke());
            };

            _client.OnClose += () =>
            {
                _isClient = false;
                Enqueue(() => OnClientDisconnect?.Invoke());
            };

            _client.OnError += (error) =>
            {
                Enqueue(() => OnClientError?.Invoke(error));
            };

            _client.OnData += (data) =>
            {
                Enqueue(() => OnClientData?.Invoke(data));
            };

            _client.OnEvent += (eventName, data) =>
            {
                Enqueue(() => OnClientEvent?.Invoke(eventName, data));
            };

            _client.ConnectToServer(ip, port);
            return _client;
        }

        /// <summary> 断开连接 </summary>
        public void DisconnectClient()
        {
            if (_client == null) return;

            OnClientConnect    = null;
            OnClientDisconnect = null;
            OnClientData       = null;
            OnClientEvent      = null;
            OnClientError      = null;

            _client.Disconnect();
            _client   = null;
            _isClient = false;
            Debug.Log("[Network] 客户端已断开");
        }

        /// <summary>
        /// 向服务器发送消息
        /// </summary>
        public void SendData(string message)
        {
            if (_client == null) 
            {
                Debug.Log("[Network] 你不是客户端，不能发送客户端消息");
                return;
            }

            var data = JsonConvert.SerializeObject(new TcpData()
            {
                dataType = "Data",
                dataContent = message,
            });
            _client.Send(data);
        }

        /// <summary>
        /// 向服务器发送事件
        /// </summary>
        public void SendEvent(string eventName, string message)
        {
            if (_client == null) 
            {
                Debug.Log("[Network] 你不是客户端，不能发送客户端事件");
                return;
            }

            var data = JsonConvert.SerializeObject(new TcpData()
            {
                dataType    = "Event",
                dataName    = eventName,
                dataContent = message,
            });
            _client.Send(data);
        }

        // ══════════════════════════════════════════════════════════════════
        //  网络工具
        // ══════════════════════════════════════════════════════════════════

        private static bool IsLanIP(IPAddress ip)
        {
            byte[] b = ip.GetAddressBytes();
            if (b[0] == 192 && b[1] == 168) return true;
            if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return true;
            if (b[0] == 10) return true;
            return false;
        }

        /// <summary> 获取本机局域网 IPv4 地址 </summary>
        public string GetLocalIP()
        {
            var allIP = Dns.GetHostAddresses(Dns.GetHostName());
            var lan   = allIP.FirstOrDefault(ip =>
                ip.AddressFamily == AddressFamily.InterNetwork && IsLanIP(ip));
            return lan?.ToString() ?? "127.0.0.1";
        }
    }
}
