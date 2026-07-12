using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;
using Cysharp.Threading.Tasks;

/// <summary>
/// 忽略 SSL 证书验证（用于自签名证书的开发环境）
/// </summary>
public class CertificateHandlerIgnore : CertificateHandler
{
    protected override bool ValidateCertificate(byte[] certificateData) => true;
}

namespace FModa.Hotfix
{
    /// <summary>
    /// 资源下载器 —— 负责服务器文件列表获取、本地文件扫描、文件对比与下载。
    /// 纯逻辑类，不依赖 MonoBehaviour，由 <see cref="HotfixManager"/> 创建并驱动。
    /// </summary>
    public class ResourcesDownload
    {
        // ── 配置 ──
        private string url;
        private string persistentPath;
        private List<FileData> serverFiles;
        private List<FileData> localFiles;
        private CancellationTokenSource _cts;
        private bool _isUpdating;   // 防重入锁

        // ── 构造 ──

        public ResourcesDownload(string persistentPath, bool isEditor, string url, string debugUrl)
        {
            this.persistentPath = persistentPath;
            this.url            = isEditor ? debugUrl : url;
            serverFiles         = new List<FileData>();
            localFiles          = new List<FileData>();
            _cts                = new CancellationTokenSource();

            if (!Directory.Exists(persistentPath))
                Directory.CreateDirectory(persistentPath);
        }

        // ── 属性 ──

        public string ServerUrl
        {
            get => url;
            set => url = value;
        }

        public int ServerFileCount => serverFiles?.Count ?? 0;
        public int LocalFileCount  => localFiles?.Count  ?? 0;

        /// <summary> 取消所有进行中的下载 </summary>
        public void Cancel()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        // ================================================================
        //  服务器文件列表
        // ================================================================

        public async UniTask GetServerFileListAsync(string childPath)
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

        public void ScanLocalFiles(string childPath)
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
        //  检查是否有待更新
        // ================================================================

        public async UniTask<bool> HasPendingUpdateAsync()
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
        //  更新子路径（获取列表 → 扫描本地 → 对比下载）
        // ================================================================

        public async UniTask UpdateChildPathAsync(string childPath)
        {
            if (_isUpdating)
            {
                Debug.LogWarning($"[{childPath}] 已有更新任务进行中，跳过");
                return;
            }
            _isUpdating = true;
            try
            {
                Debug.Log($">>> 更新 [{childPath}]");
                await GetServerFileListAsync(childPath);
                ScanLocalFiles(childPath);
                await CompareAndDownloadAsync(childPath);
            }
            finally { _isUpdating = false; }
        }

        // ================================================================
        //  更新 AB 包
        // ================================================================

        public async UniTask UpdateABAsync()
        {
            if (_isUpdating)
            {
                Debug.LogWarning($"已有更新任务进行中，跳过 AB 更新");
                return;
            }
            _isUpdating = true;
            try
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
            finally { _isUpdating = false; }
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
        //  下载单个 AB 包（外部调用）
        // ================================================================

        public async UniTask DownloadABAsync(string abName)
        {
            string folderName = GetPlatformABFolder();
            string baseUrl    = $"{url}/{folderName}";
            var    loader     = new LoadAB(baseUrl);
            await loader.DownloadABAsync(abName);
        }

        // ================================================================
        //  工具方法
        // ================================================================

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
        //  版本文件解析
        // ================================================================

        /// <summary> 从服务器下载并解析 version.json </summary>
        public async UniTask<VersionData> GetRemoteVersionAsync()
        {
            string remoteUrl = $"{url}/version/version.json".Replace("\\", "/");
            using (UnityWebRequest req = UnityWebRequest.Get(remoteUrl))
            {
                req.certificateHandler = new CertificateHandlerIgnore();
                await req.SendWebRequest().ToUniTask(cancellationToken: _cts.Token);

                if (req.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var ver = JsonUtility.FromJson<VersionData>(req.downloadHandler.text);
                        if (ver != null) return ver;
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"解析远程 version.json 失败: {e.Message}");
                    }
                }
                else
                {
                    Debug.LogError($"下载远程 version.json 失败 [{remoteUrl}]: {req.error}");
                }
            }
            return null;
        }

        /// <summary> 读取本地 version.json </summary>
        public VersionData GetLocalVersion()
        {
            string localPath = Path.Combine(persistentPath, "version", "version.json");
            if (!File.Exists(localPath)) return null;
            try
            {
                return JsonUtility.FromJson<VersionData>(File.ReadAllText(localPath));
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"读取本地 version.json 失败: {e.Message}");
                return null;
            }
        }

        /// <summary> 将 version.json 保存到本地 </summary>
        public void SaveLocalVersion(VersionData data)
        {
            string dir  = Path.Combine(persistentPath, "version");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "version.json"), JsonUtility.ToJson(data, true));
        }

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
