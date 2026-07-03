using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;
using XLua;
using Cysharp.Threading.Tasks;

/// <summary>
/// 忽略 SSL 证书验证（用于自签名证书的开发环境）
/// </summary>
public class CertificateHandlerIgnore : CertificateHandler
{
    protected override bool ValidateCertificate(byte[] certificateData)
    {
        return true;
    }
}

namespace ECMVC.HotUpdate
{
    [Hotfix]
    public class HotUpdateManager : MonoBehaviour,IHotUpdate
    {
        public static HotUpdateManager Instance;

        private LuaEnv luaEnv;

        [SerializeField]
        private string url = @"https://frp-gap.com:64849";

        [SerializeField]
        private string debugUrl = @"https://localhost:6567";

        private string persistentPath;
        private List<FileData> serverFiles;
        private List<FileData> localFiles;
        private CancellationTokenSource _cts;

        // ────────── 公开属性 ──────────

        public string ServerUrl
        {
            get => url;
            set => url = value;
        }

        public int ServerFileCount => serverFiles?.Count ?? 0;
        public int LocalFileCount  => localFiles?.Count  ?? 0;

        string IHotUpdate.url { get => url; set => url = value; }
        string IHotUpdate.debugUrl { get => debugUrl; set => debugUrl = value; }

        // ================================================================
        //  Unity 生命周期
        // ================================================================


        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            _cts            = new CancellationTokenSource();//取消令牌
            luaEnv          = new LuaEnv();//Lua 环境
            persistentPath  = Application.persistentDataPath;//持久化路径
            serverFiles     = new List<FileData>();//服务器文件列表
            localFiles      = new List<FileData>();//本地文件列表

            if (!Directory.Exists(persistentPath))
                Directory.CreateDirectory(persistentPath);

            if (Application.isEditor)
                url = debugUrl;
        }

        void Start()
        {
            CheckAndUpdateAsync().Forget();
        }

        void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();

            serverFiles?.Clear();
            localFiles?.Clear();
            luaEnv?.Dispose();
        }

        // ================================================================
        //  公开方法（供测试窗口 / 外部调用）
        // ================================================================

        public void StartCheckUpdate()             => CheckAndUpdateAsync().Forget();
        public void StartFullUpdate()              => UpdateResourceAsync().Forget();
        public void StartGetFileList(string path)  => GetServerFileListAsync(path).Forget();
        public void StartScanLocal(string childPath) => ScanLocalFiles(childPath);
        public void StartCompareDownload(string childPath) => CompareAndDownloadAsync(childPath).Forget();

        // ================================================================
        //  完整更新流程
        // ================================================================

        public async UniTask UpdateResourceAsync()
        {
            await UniTask.Yield();
            Debug.Log("=== XLua 资源更新开始 ===");

            await UpdateChildPathAsync("version");
            await UpdateChildPathAsync("lua");

            SetupLuaEnvironment();

            await UpdateABAsync();

            Debug.Log("=== XLua 资源更新完成 ===");
        }

        // ================================================================
        //  内部流程
        // ================================================================

        private async UniTask CheckAndUpdateAsync()
        {
            try
            {
                Debug.Log("检查资源更新...");

                await GetServerFileListAsync("version");
                ScanLocalFiles("version");

                bool needUpdate = await HasPendingUpdateAsync();

                if (needUpdate)
                {
                    Debug.Log("检测到新版本，自动开始更新...");
                    await UpdateResourceAsync();
                }
                else
                {
                    Debug.Log("所有资源已是最新版本");
                    SetupLuaEnvironment();
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[XLuaManager] 检查更新失败: {e.Message}");
                SetupLuaEnvironment();
            }
        }

        private async UniTask UpdateChildPathAsync(string childPath)
        {
            Debug.Log($">>> 更新 [{childPath}]");
            await GetServerFileListAsync(childPath);
            ScanLocalFiles(childPath);
            await CompareAndDownloadAsync(childPath);
        }

        private async UniTask UpdateABAsync()
        {
            string abChildPath;
            switch (Application.platform)
            {
                case RuntimePlatform.Android:      abChildPath = "AssetBundles_Android"; break;
                case RuntimePlatform.IPhonePlayer: abChildPath = "AssetBundles_IOS";     break;
                default:                           abChildPath = "AssetBundles_Windows"; break;
            }

            Debug.Log($">>> 更新 AB 包 [{abChildPath}]");
            await GetServerFileListAsync(abChildPath);
            ScanLocalFiles(abChildPath);
            await DownloadABFilesAsync(abChildPath);
        }

        private async UniTask<bool> HasPendingUpdateAsync()
        {
            // 服务器有但本地没有 / 不一致
            foreach (var sf in serverFiles)
            {
                var lf = localFiles.Find(f => f.name == sf.name);
                if (lf == null || lf.md5 != sf.md5 || lf.size != sf.size)
                    return true;
            }
            // 本地有但服务器已删除
            foreach (var lf in localFiles)
            {
                if (!serverFiles.Exists(f => f.name == lf.name))
                    return true;
            }
            return false;
        }

        // ================================================================
        //  服务器文件列表
        // ================================================================

        private async UniTask GetServerFileListAsync(string childPath)
        {
            serverFiles.Clear();

            string apiUrl = $"{url}/api/files?childPath={childPath}";

            using (UnityWebRequest req = UnityWebRequest.Get(apiUrl))
            {
                req.certificateHandler = new CertificateHandlerIgnore();
                await req.SendWebRequest().ToUniTask(cancellationToken: _cts.Token);

                if (req.result == UnityWebRequest.Result.Success)
                {
                    var resp = JsonUtility.FromJson<FileListResponse>(req.downloadHandler.text);
                    if (resp.success)
                    {
                        serverFiles = resp.files ?? new List<FileData>();
                        Debug.Log($"服务器 [{childPath}]: {serverFiles.Count} 个文件");
                        foreach (var f in serverFiles)
                            Debug.Log($"  {f.name}  ({f.size} bytes, MD5:{f.md5})");
                    }
                    else
                    {
                        Debug.LogError($"服务器返回错误: {resp.error}");
                    }
                }
                else
                {
                    Debug.LogError($"网络请求失败 [{apiUrl}]: {req.error}");
                }
            }
        }

        // ================================================================
        //  本地文件扫描
        // ================================================================

        void ScanLocalFiles(string childPath)
        {
            localFiles.Clear();
            var dir = Path.Combine(persistentPath, childPath);

            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
                Debug.Log($"本地 [{childPath}]: 目录为空，已创建");
                return;
            }

            foreach (string fp in Directory.GetFiles(dir))
            {
                string name = Path.GetFileName(fp);
                if (name.StartsWith(".")) continue;

                var fi = new FileInfo(fp);
                localFiles.Add(new FileData
                {
                    name         = name,
                    size         = fi.Length,
                    md5          = CalculateMD5(fp),
                    lastModified = fi.LastWriteTime.Ticks,
                });
            }

            Debug.Log($"本地 [{childPath}]: {localFiles.Count} 个文件");
        }

        // ================================================================
        //  对比并下载（通用文件）
        // ================================================================

        public async UniTask CompareAndDownloadAsync(string childPath)
        {
            if (serverFiles.Count == 0)
            {
                Debug.Log($"[{childPath}] 服务器文件列表为空，跳过下载");
                return;
            }

            int updated = 0;

            foreach (var sf in serverFiles)
            {
                var lf   = localFiles.Find(f => f.name == sf.name);
                bool need  = false;
                string why = "";

                if (lf == null)               { need = true; why = "新文件"; }
                else if (lf.md5 != sf.md5)    { need = true; why = "MD5 变更"; }
                else if (lf.size != sf.size)  { need = true; why = "大小变化"; }

                if (need)
                {
                    Debug.Log($"  下载: {sf.name}（{why}）");
                    await DownloadFileAsync(childPath, sf.name);
                    updated++;
                }
                else
                {
                    Debug.Log($"  已最新: {sf.name}");
                }
            }

            // 删除本地多余文件
            foreach (var lf in localFiles)
            {
                if (!serverFiles.Exists(f => f.name == lf.name))
                {
                    string delPath = Path.Combine(persistentPath, childPath, lf.name);
                    Debug.Log($"  删除多余文件: {lf.name}");
                    if (File.Exists(delPath)) File.Delete(delPath);
                }
            }

            Debug.Log($"[{childPath}] 更新完成，共处理 {updated} 个文件");
        }

        // ================================================================
        //  下载 AB 包（使用 LoadAB 的流式下载）
        // ================================================================

        private async UniTask DownloadABFilesAsync(string childPath)
        {
            string baseUrl = $"{url}/{childPath}".Replace("\\", "/");
            string saveDir = Path.Combine(persistentPath, childPath);
            int updated = 0;

            foreach (var sf in serverFiles)
            {
                // 跳过 .manifest 和 .meta 文件，只下载真正的 AB 包
                string lower = sf.name.ToLower();
                if (lower.EndsWith(".manifest") || lower.EndsWith(".meta")) continue;

                var lf   = localFiles.Find(f => f.name == sf.name);
                bool need  = false;
                string why = "";

                if (lf == null)               { need = true; why = "新文件"; }
                else if (lf.md5 != sf.md5)    { need = true; why = "MD5 变更"; }
                else if (lf.size != sf.size)  { need = true; why = "大小变化"; }

                if (need)
                {
                    Debug.Log($"  下载 AB: {sf.name}（{why}）");
                    var loader = new LoadAB(baseUrl);
                    await loader.DownloadABAsync(sf.name);
                    updated++;
                }
                else
                {
                    Debug.Log($"  AB 已最新: {sf.name}");
                }
            }

            // 删除本地多余 AB 文件
            foreach (var lf in localFiles)
            {
                if (!serverFiles.Exists(f => f.name == lf.name))
                {
                    string delPath = Path.Combine(saveDir, lf.name);
                    Debug.Log($"  删除多余 AB: {lf.name}");
                    if (File.Exists(delPath)) File.Delete(delPath);
                }
            }

            Debug.Log($"[AB] 下载完成，共 {updated} 个");
        }

        // ================================================================
        //  下载单个文件
        // ================================================================

        private async UniTask DownloadFileAsync(string childPath, string name)
        {
            string remote = $"{url}/{childPath}/{name}".Replace("\\", "/");
            string local  = Path.Combine(persistentPath, childPath, name);

            // 确保目录存在
            Directory.CreateDirectory(Path.GetDirectoryName(local));

            using (UnityWebRequest req = UnityWebRequest.Get(remote))
            {
                req.certificateHandler = new CertificateHandlerIgnore();
                await req.SendWebRequest().ToUniTask(cancellationToken: _cts.Token);

                if (req.result == UnityWebRequest.Result.Success)
                {
                    File.WriteAllBytes(local, req.downloadHandler.data);
                    Debug.Log($"  已保存: {name} ({req.downloadHandler.data.Length} bytes)");
                }
                else
                {
                    Debug.LogError($"  下载失败 {name}: {req.error}");
                }
            }
        }

        // ================================================================
        //  Lua 环境
        // ================================================================

        public void SetupLuaEnvironment()
        {
            LoadAB.SetupLuaEnvironment(luaEnv, persistentPath);
        }

        /// <summary> 加载指定名称的 AB 包，返回包内所有资源</summary>
        public async UniTask<object[]> LoadABAsync(string abName, bool isLocal = false)
        {
            string folderName = GetPlatformABFolder();
            string baseUrl    = $"{url}/{folderName}";
            var    loader     = new LoadAB(baseUrl);
            return await loader.LoadABAsync(abName, false, isLocal);
        }

        /// <summary> 从服务器下载指定名称的 AB 包</summary>
        public async UniTask DownloadABAsync(string abName)
        {
            string folderName = GetPlatformABFolder();
            string baseUrl    = $"{url}/{folderName}";
            var    loader     = new LoadAB(baseUrl);
            await loader.DownloadABAsync(abName);
        }

        private static string GetPlatformABFolder()
        {
            switch (Application.platform)
            {
                case RuntimePlatform.Android:      return "AssetBundles_Android";
                case RuntimePlatform.IPhonePlayer: return "AssetBundles_IOS";
                default:                           return "AssetBundles_Windows";
            }
        }

        // ================================================================
        //  工具方法
        // ================================================================

        private string CalculateMD5(string filePath)
        {
            try
            {
                using (var md5    = System.Security.Cryptography.MD5.Create())
                using (var stream = File.OpenRead(filePath))
                {
                    byte[] hash = md5.ComputeHash(stream);
                    return System.BitConverter.ToString(hash).Replace("-", "").ToLower();
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"MD5 计算失败 {filePath}: {e.Message}");
                return "";
            }
        }
    }
}
