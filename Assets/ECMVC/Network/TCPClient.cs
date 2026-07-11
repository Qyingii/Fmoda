using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Events;

namespace ECMVC.Net
{
    [Serializable]
    public class ClientIdentityData
    {
        public string serverKey;      // "ip:port"
        public string serverIp;
        public int serverPort;
        public string clientId;
        public long expireAtTicks;
    }

    public class TCPClient : TcpClient
    {
        private TcpClient client;
        private NetworkStream stream;
        private Thread receiveThread;
        private volatile bool _disconnecting = false; // 协作式取消标志
        private bool isConnected = false;

        private List<byte> dataBuffer = new List<byte>();
        private int expectedMessageLength = 0;
        private bool readingLength = true;
        private byte[] buffer = new byte[1024];

        private string ip = "127.0.0.1";
        private int port = 9999;

        // ── 客户端身份 ──
        private string _clientId = "";
        private string _savedToken = ""; // 从本地读到的令牌
        private const int TTL_HOURS = 24;

        public UnityAction OnOpen = () => { };
        public UnityAction OnClose = () => { };
        public UnityAction<string> OnData = (data) => { };
        public UnityAction<string, string> OnEvent = (eventName, data) => { };
        public UnityAction<Exception> OnError = (error) => { };

        public string IP { get => ip; }
        public int Port { get => port; }
        public bool IsConnected { get => isConnected; }
        public string ClientId => _clientId;

        // ── 身份文件路径 ──────────────────────────────────────────────────

        private static string IdentityFilePath =>
            Path.Combine(Application.persistentDataPath, "client_identities.json");

        // ── 多身份管理 ─────────────────────────────────────────────────────

        private List<ClientIdentityData> LoadAllIdentities()
        {
            if (!File.Exists(IdentityFilePath))
                return new List<ClientIdentityData>();
            try
            {
                string json = File.ReadAllText(IdentityFilePath);
                return JsonConvert.DeserializeObject<List<ClientIdentityData>>(json)
                       ?? new List<ClientIdentityData>();
            }
            catch
            {
                File.Delete(IdentityFilePath);
                return new List<ClientIdentityData>();
            }
        }

        private void SaveAllIdentities(List<ClientIdentityData> identities)
        {
            identities.RemoveAll(id => DateTime.UtcNow.Ticks > id.expireAtTicks);
            File.WriteAllText(IdentityFilePath, JsonConvert.SerializeObject(identities));
        }

        /// <summary> 查找指定服务器的已保存令牌（自动检查 TTL）</summary>
        private string FindSavedToken(string serverIp, int serverPort)
        {
            string key = $"{serverIp}:{serverPort}";
            var identities = LoadAllIdentities();

            foreach (var id in identities)
            {
                if (id.serverKey != key) continue;
                if (DateTime.UtcNow.Ticks > id.expireAtTicks)
                {
                    Debug.Log($"服务器 {key} 的令牌已过期，清除");
                    identities.Remove(id);
                    SaveAllIdentities(identities);
                    return null;
                }
                return id.clientId;
            }
            return null;
        }

        /// <summary> 保存或更新指定服务器的令牌 </summary>
        private void SaveToken(string serverIp, int serverPort, string clientId)
        {
            string key = $"{serverIp}:{serverPort}";
            var identities = LoadAllIdentities();
            identities.RemoveAll(id => id.serverKey == key);

            identities.Add(new ClientIdentityData
            {
                serverKey     = key,
                serverIp      = serverIp,
                serverPort    = serverPort,
                clientId      = clientId,
                expireAtTicks = DateTime.UtcNow.AddHours(TTL_HOURS).Ticks,
            });

            SaveAllIdentities(identities);
            Debug.Log($"令牌已保存: {key} → {clientId}");
        }

        private void DeleteToken(string serverIp, int serverPort)
        {
            string key = $"{serverIp}:{serverPort}";
            var identities = LoadAllIdentities();
            if (identities.RemoveAll(id => id.serverKey == key) > 0)
            {
                SaveAllIdentities(identities);
                Debug.Log($"令牌已删除: {key}");
            }
        }

        // ── 接收数据（后台线程） ────────────────────────────────────────────

        private void ReceiveData()
        {
            while (isConnected && !_disconnecting)
            {
                try
                {
                    int streamCount;
                    if (readingLength)
                        streamCount = stream.Read(buffer, 0, 4);
                    else
                    {
                        int len = Mathf.Min(expectedMessageLength, buffer.Length);
                        streamCount = stream.Read(buffer, 0, len);
                    }

                    if (streamCount > 0)
                        OnDataReceived(streamCount);
                }
                catch (ThreadAbortException)
                {
                    // Editor 停止播放时线程被强制终止 — 正常行为，不记录
                    break;
                }
                catch (Exception e)
                {
                    if (!_disconnecting)
                        Debug.LogError("接收消息失败: " + e.Message);
                    break;
                }
            }
        }

        private void OnDataReceived(int bytesRead)
        {
            try
            {
                dataBuffer.AddRange(buffer.Take(bytesRead).ToArray());

                if (readingLength)
                {
                    if (dataBuffer.Count == 4)
                    {
                        expectedMessageLength = BitConverter.ToInt32(dataBuffer.ToArray(), 0);
                        readingLength = false;
                        dataBuffer.Clear();
                    }
                    else
                    {
                        int n = stream.Read(buffer, 0, 4 - bytesRead);
                        OnDataReceived(n);
                    }
                }
                else
                {
                    if (dataBuffer.Count < expectedMessageLength)
                    {
                        int n = Mathf.Min(expectedMessageLength - dataBuffer.Count, buffer.Length);
                        int r = stream.Read(buffer, 0, n);
                        OnDataReceived(r);
                    }
                    else
                    {
                        string receivedJson = Encoding.UTF8.GetString(dataBuffer.ToArray(), 0, expectedMessageLength);

                        NetworkManager.Enqueue(() =>
                        {
                            var data = JsonConvert.DeserializeObject<TcpData>(receivedJson);
                            if (data.dataType == "Data")
                                OnData?.Invoke(data.dataContent);
                            else if (data.dataType == "Event")
                                OnEvent?.Invoke(data.dataName, data.dataContent);
                        });

                        readingLength = true;
                        dataBuffer.Clear();
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError("接收数据错误: " + e.Message);
            }
        }

        // ── 连接服务器 ─────────────────────────────────────────────────────

        public void ConnectToServer(string ip, int port = 9999)
        {
            try
            {
                this.ip = ip;
                this.port = port;

                // 1. 查找本地是否已有此服务器的令牌
                _savedToken = FindSavedToken(ip, port) ?? "";
                if (!string.IsNullOrEmpty(_savedToken))
                    Debug.Log($"找到已保存的令牌: {_savedToken}");

                // 2. 建立 TCP 连接（带超时，避免阻塞主线程过久）
                client = new System.Net.Sockets.TcpClient();
                var connectTask = client.ConnectAsync(ip, port);
                if (!connectTask.Wait(5000)) // 5 秒超时
                {
                    Debug.LogError("连接超时");
                    OnError?.Invoke(new TimeoutException("连接超时"));
                    return;
                }
                stream = client.GetStream();
                isConnected = true;

                // 3. 注册事件处理器
                OnEvent += (eventName, eventData) =>
                {
                    // ── 服务端告知可以发送令牌 ──
                    if (eventName == "SendToken")
                    {
                        Debug.Log("服务端请求令牌，发送本地令牌...");
                        var helloData = JsonConvert.SerializeObject(new TcpData()
                        {
                            dataType    = "Event",
                            dataName    = "ClientHello",
                            dataContent = _savedToken,
                        });
                        Send(helloData);
                        return;
                    }

                    // ── 服务端返回令牌验证结果 ──
                    if (eventName == "ConnectSussess")
                    {
                        var obj = JsonConvert.DeserializeObject<Dictionary<string, object>>(eventData);
                        if (obj == null) return;

                        string responseClientId = obj["clientId"]?.ToString() ?? "";
                        string status           = obj["status"]?.ToString() ?? "";

                        if (!string.IsNullOrEmpty(responseClientId))
                        {
                            // 服务端发来了新令牌 → 保存
                            SaveToken(ip, port, responseClientId);
                            _clientId = responseClientId;
                            Debug.Log($"收到新令牌并保存: {responseClientId}");
                        }
                        else
                        {
                            // 空令牌 → 现有令牌有效，无需更新
                            _clientId = _savedToken;
                            Debug.Log("令牌验证通过，继续使用现有令牌");
                        }
                        return;
                    }

                    // ── 服务器关闭通知 ──
                    if (eventName == "ServerShutdown")
                    {
                        Debug.Log("收到服务器关闭通知，清除该服务器令牌");
                        DeleteToken(ip, port);
                        isConnected = false;
                        OnClose?.Invoke();
                        Disconnect();
                    }
                };

                // 4. 启动接收线程
                receiveThread = new Thread(ReceiveData);
                receiveThread.IsBackground = true;
                receiveThread.Start();

                OnOpen?.Invoke();
            }
            catch (Exception e)
            {
                OnError?.Invoke(e);
                Debug.LogError("连接失败: " + e.Message);
            }
        }

        // ── 发送消息 ──────────────────────────────────────────────────────

        public void Send(string message)
        {
            if (!isConnected) return;
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(message);
                byte[] len  = BitConverter.GetBytes(data.Length);
                stream.Write(len, 0, len.Length);
                stream.Write(data, 0, data.Length);
            }
            catch (Exception e)
            {
                OnError?.Invoke(e);
                Debug.LogError("发送失败: " + e.Message);
            }
        }

        // ── 断开连接 ──────────────────────────────────────────────────────

        public void Disconnect()
        {
            if (!isConnected) return;

            _disconnecting = true;
            OnClose?.Invoke();
            isConnected = false;
            stream?.Close();
            client?.Close();
            OnOpen = null;
            OnData = null;
            OnEvent = null;
            OnError = null;
            OnClose = null;

            Debug.Log("连接已断开（令牌保留）");
        }
    }
}
