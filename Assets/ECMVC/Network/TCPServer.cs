using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Events;

namespace ECMVC.Net
{
    public class TCPServer
    {
        private TcpListener listener;
        private bool isRunning;
        private readonly List<TcpClient> clients = new List<TcpClient>();
        private object clientsLock = new object();

        public UnityAction<TcpClient> OnClientOpen = (client) => { };
        public UnityAction<TcpClient> OnClientClose = (client) => { };
        public UnityAction<TcpClient, string> OnClientData = (client, data) => { };
        public UnityAction<TcpClient, string, string> OnClientEvent = (client, eventName, data) => { };
        public UnityAction<TcpClient, Exception> OnClientError = (client, error) => { };

        public UnityAction OnOpen = () => { };
        public UnityAction OnClose = () => { };
        public UnityAction<Exception> OnError = (error) => { };

        public List<TcpClient> Clients { get => clients; }

        private int nextClientId = 0;
        private Dictionary<int, ClientState> clientStates = new Dictionary<int, ClientState>();

        // ── 令牌管理 ────────────────────────────────────────────────────────

        private const int TOKEN_TTL_HOURS = 24;

        // token → 过期时间，服务端重启后此字典清空，所有旧令牌失效
        private Dictionary<string, long> _issuedTokens = new Dictionary<string, long>();

        private class ClientState
        {
            public TcpClient Client { get; set; }
            public NetworkStream Stream { get; set; }
            public int ClientId { get; set; }
            public string ClientHashId { get; set; }
            public bool HandshakeDone { get; set; } = false;
            public byte[] Buffer { get; set; } = new byte[1024];
            public List<byte> DataBuffer { get; set; } = new List<byte>();
            public bool ReadingLength { get; set; } = true;
            public int ExpectedMessageLength { get; set; }

            public ClientState(TcpClient client, int clientId, string clientHashId)
            {
                Client = client;
                Stream = client.GetStream();
                ClientId = clientId;
                ClientHashId = clientHashId;
            }
        }

        // ── 生成哈希令牌 ────────────────────────────────────────────────────

        private string GenerateToken(TcpClient client)
        {
            string raw = $"{client.Client.RemoteEndPoint}_{DateTime.UtcNow.Ticks}_{nextClientId}_{Guid.NewGuid():N}";
            using (var md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(raw));
                return BitConverter.ToString(hash).Replace("-", "").ToLower();
            }
        }

        // ── 令牌校验 ────────────────────────────────────────────────────────

        /// <summary> 验证客户端令牌，有效刷新 TTL，无效返回 false </summary>
        private bool TryReuseToken(string token)
        {
            if (string.IsNullOrEmpty(token)) return false;
            if (!_issuedTokens.TryGetValue(token, out long expireAt)) return false;

            if (DateTime.UtcNow.Ticks > expireAt)
            {
                _issuedTokens.Remove(token);
                return false;
            }

            // 有效，刷新 TTL
            _issuedTokens[token] = DateTime.UtcNow.AddHours(TOKEN_TTL_HOURS).Ticks;
            return true;
        }

        /// <summary> 签发新令牌 </summary>
        private string IssueToken(TcpClient client)
        {
            string token = GenerateToken(client);
            _issuedTokens[token] = DateTime.UtcNow.AddHours(TOKEN_TTL_HOURS).Ticks;
            return token;
        }

        // ── 启动服务器 ───────────────────────────────────────────────────────

        public void StartServer(int port = 9999)
        {
            try
            {
                // ── ClientHello → 校验令牌 / 签发新令牌 ──
                OnClientEvent += (tcpClient, eventName, data) =>
                {
                    if (eventName == "ClientHello")
                    {
                        ClientState state = null;
                        lock (clientsLock)
                        {
                            foreach (var kv in clientStates)
                                if (kv.Value.Client == tcpClient) { state = kv.Value; break; }
                        }
                        if (state == null || state.HandshakeDone) return;
                        state.HandshakeDone = true;

                        string savedToken = data;
                        string responseClientId; // 空字符串 = 令牌有效，无需更新
                        string responseStatus;

                        if (TryReuseToken(savedToken))
                        {
                            // 令牌有效 → 返回空，客户端继续使用现有令牌
                            responseClientId = "";
                            responseStatus   = "reused";
                            state.ClientHashId = savedToken;
                            Debug.Log($"客户端 {state.ClientId} 令牌验证通过");
                        }
                        else
                        {
                            // 无令牌或无效 → 签发新令牌
                            responseClientId = IssueToken(tcpClient);
                            responseStatus   = "new";
                            state.ClientHashId = responseClientId;
                            if (string.IsNullOrEmpty(savedToken))
                                Debug.Log($"客户端 {state.ClientId} 无令牌，签发新身份: {responseClientId}");
                            else
                                Debug.Log($"客户端 {state.ClientId} 令牌无效，重新签发: {responseClientId}");
                        }

                        var result = JsonConvert.SerializeObject(new
                        {
                            clientId = responseClientId,
                            status   = responseStatus,
                        });
                        var msg = JsonConvert.SerializeObject(new TcpData()
                        {
                            dataType    = "Event",
                            dataName    = "ConnectSussess",
                            dataContent = result,
                        });
                        SendMessage(msg, tcpClient);
                    }
                };

                OnClientClose += (client) =>
                {
                    Debug.Log("客户端" + client + "连接关闭");
                };

                if (IsPortInUse(port))
                {
                    Debug.LogError($"端口 {port} 已被占用，尝试使用端口 {port + 1}");
                    StartServer(port + 1);
                    return;
                }

                listener = new TcpListener(IPAddress.Any, port);
                listener.Start();
                isRunning = true;

                Thread listenThread = new Thread(ListenForClients);
                listenThread.IsBackground = true;
                listenThread.Start();

                OnOpen?.Invoke();
            }
            catch (Exception e)
            {
                OnError?.Invoke(e);
                Debug.Log("服务器启动出错: " + e.Message);
            }
        }

        private bool IsPortInUse(int port)
        {
            try
            {
                using (var client = new TcpClient())
                {
                    client.Connect("127.0.0.1", port);
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        // ── 接受客户端连接 ──────────────────────────────────────────────────

        private void ListenForClients()
        {
            while (isRunning)
            {
                try
                {
                    TcpClient client = listener.AcceptTcpClient();
                    Debug.Log("新客户端连接...");

                    string tempId;
                    int clientIdx;

                    lock (clientsLock)
                    {
                        clientIdx = nextClientId++;
                        tempId = $"temp_{clientIdx}_{Guid.NewGuid():N}";
                        clients.Add(client);
                        clientStates.Add(clientIdx, new ClientState(client, clientIdx, tempId));
                    }

                    // 通知 NetworkManager 有新客户端连接
                    OnClientOpen?.Invoke(client);

                    // 告知客户端可以发送令牌
                    var sendTokenMsg = JsonConvert.SerializeObject(new TcpData()
                    {
                        dataType = "Event",
                        dataName = "SendToken",
                        dataContent = "",
                    });
                    SendMessage(sendTokenMsg, client);

                    Thread clientThread = new Thread(() => HandleClient(clientStates[clientIdx]));
                    clientThread.IsBackground = true;
                    clientThread.Start();
                }
                catch (Exception e)
                {
                    // 服务器关闭时 listener.Stop() 会中断 AcceptTcpClient() — 这是正常行为
                    if (isRunning)
                        Debug.LogError("接受客户端连接失败: " + e.Message);
                }
            }
        }

        // ── 后续：HandleClient / OnDataReceived / SendMessage / StopServer ──

        private void HandleClient(ClientState state)
        {
            NetworkStream stream = state.Stream;

            while (isRunning && IsClientConnected(state.Client))
            {
                try
                {
                    int streamCount;
                    if (state.ReadingLength)
                        streamCount = stream.Read(state.Buffer, 0, 4);
                    else
                    {
                        var len = Mathf.Min(state.ExpectedMessageLength, state.Buffer.Length);
                        streamCount = stream.Read(state.Buffer, 0, len);
                    }

                    if (streamCount > 0)
                        OnDataReceived(state, streamCount);
                }
                catch (IOException) { break; }
                catch (ObjectDisposedException) { break; }
                catch (ThreadAbortException)
                {
                    // Editor 停止播放时线程被强制终止 — 正常行为
                    break;
                }
                catch (Exception e)
                {
                    OnClientError?.Invoke(state.Client, e);
                    break;
                }
            }

            CleanupClient(state);
        }

        private bool IsClientConnected(TcpClient client)
        {
            if (client == null || !client.Connected) return false;
            try
            {
                if (client.Client.Poll(0, SelectMode.SelectRead))
                {
                    byte[] buf = new byte[1];
                    if (client.Client.Receive(buf, SocketFlags.Peek) == 0)
                        return false;
                }
            }
            catch { return false; }
            return true;
        }

        private void CleanupClient(ClientState state)
        {
            NetworkManager.Enqueue(() => OnClientClose?.Invoke(state.Client));
            Debug.Log("客户端断开");
            try { state.Stream?.Close(); } catch { }
            try { state.Client?.Close(); } catch { }
            lock (clientsLock)
            {
                clients.Remove(state.Client);
                clientStates.Remove(state.ClientId);
            }
        }

        private void OnDataReceived(ClientState state, int bytesRead)
        {
            try
            {
                state.DataBuffer.AddRange(state.Buffer.Take(bytesRead).ToArray());

                if (state.ReadingLength)
                {
                    if (state.DataBuffer.Count == 4)
                    {
                        state.ExpectedMessageLength = BitConverter.ToInt32(state.DataBuffer.ToArray(), 0);
                        state.ReadingLength = false;
                        state.DataBuffer.Clear();
                    }
                    else
                    {
                        int n = state.Stream.Read(state.Buffer, 0, 4 - bytesRead);
                        OnDataReceived(state, n);
                    }
                }
                else
                {
                    if (state.DataBuffer.Count < state.ExpectedMessageLength)
                    {
                        int n = Mathf.Min(state.ExpectedMessageLength - state.DataBuffer.Count, state.Buffer.Length);
                        int r = state.Stream.Read(state.Buffer, 0, n);
                        OnDataReceived(state, r);
                    }
                    else
                    {
                        string json = Encoding.UTF8.GetString(state.DataBuffer.ToArray(), 0, state.ExpectedMessageLength);
                        NetworkManager.Enqueue(() =>
                        {
                            var d = JsonConvert.DeserializeObject<TcpData>(json);
                            if (d.dataType == "Data")
                                OnClientData?.Invoke(state.Client, d.dataContent);
                            else if (d.dataType == "Event")
                                OnClientEvent?.Invoke(state.Client, d.dataName, d.dataContent);
                        });
                        state.ReadingLength = true;
                        state.DataBuffer.Clear();
                    }
                }
            }
            catch (Exception e) { Debug.LogError("接收数据错误: " + e.Message); }
        }

        public void SendMessage(string message, TcpClient client)
        {
            if (client == null) return;
            try
            {
                var stream = client.GetStream();
                byte[] buf = Encoding.UTF8.GetBytes(message);
                byte[] len = BitConverter.GetBytes(buf.Length);
                stream.Write(len, 0, len.Length);
                stream.Write(buf, 0, buf.Length);
            }
            catch (Exception e) { Debug.Log("发送消息出错: " + e.Message); }
        }

        public void StopServer()
        {
            var shutdownMsg = JsonConvert.SerializeObject(new TcpData()
            {
                dataType = "Event",
                dataName = "ServerShutdown",
                dataContent = "",
            });
            lock (clientsLock)
                foreach (var c in clients)
                    try { SendMessage(shutdownMsg, c); } catch { }

            isRunning = false;
            OnClose?.Invoke();
            listener?.Stop();
            _issuedTokens.Clear();
            Debug.Log("服务器关闭");
        }
    }
}
