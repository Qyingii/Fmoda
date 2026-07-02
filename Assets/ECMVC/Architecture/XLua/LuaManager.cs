using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using XLua;

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

namespace ECMVC.XLua
{
    [Hotfix]
    public class LuaManager : MonoBehaviour
    {
        public static LuaManager Instance;

        private LuaEnv luaEnv;

        [SerializeField]
        private string url = @"https://frp-gap.com:64849";

        [SerializeField]
        private string debugUrl = @"https://localhost:6567";

        private string persistentPath;
        private List<FileData> serverFiles;
        private List<FileData> localFiles;

        // ────────── 公开属性 ──────────

        public string ServerUrl
        {
            get => url;
            set => url = value;
        }

        public int ServerFileCount => serverFiles?.Count ?? 0;
        public int LocalFileCount  => localFiles?.Count  ?? 0;

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

            luaEnv          = new LuaEnv();
            persistentPath  = Application.persistentDataPath;
            serverFiles     = new List<FileData>();
            localFiles      = new List<FileData>();

            if (!Directory.Exists(persistentPath))
                Directory.CreateDirectory(persistentPath);

            if (Application.isEditor)
                url = debugUrl;
        }

        void Start()
        {
#if UNITY_EDITOR
            Debug.Log("[XLuaManager] Editor 模式：请在测试窗口中手动触发更新检查");
#else
            StartCoroutine(CheckAndUpdate());
#endif
        }

        void OnDestroy()
        {
            serverFiles?.Clear();
            localFiles?.Clear();
            luaEnv?.Dispose();
        }

        // ================================================================
        //  公开方法（供测试窗口 / 外部调用）
        // ================================================================

        /// <summary> 检查并自动更新（协程）</summary>
        public void StartCheckUpdate()             => StartCoroutine(CheckAndUpdate());

        /// <summary> 完整更新流程（协程）</summary>
        public void StartFullUpdate()              => StartCoroutine(UpdateResource());

        /// <summary> 获取服务器文件列表（协程）</summary>
        public void StartGetFileList(string path)  => StartCoroutine(GetServerFileList(path));

        /// <summary> 扫描本地文件（同步）</summary>
        public void StartScanLocal(string childPath) => ScanLocalFiles(childPath);

        /// <summary> 对比服务器/本地并下载（协程）</summary>
        public void StartCompareDownload(string childPath) => StartCoroutine(CompareAndDownload(childPath));

        // ================================================================
        //  完整更新流程
        // ================================================================

        public IEnumerator UpdateResource()
        {
            yield return null;
            Debug.Log("=== XLua 资源更新开始 ===");

            yield return StartCoroutine(UpdateChildPath("version"));
            yield return StartCoroutine(UpdateChildPath("lua"));

            SetupLuaEnvironment();

            yield return StartCoroutine(UpdateAB());

            Debug.Log("=== XLua 资源更新完成 ===");
        }

        // ================================================================
        //  内部流程
        // ================================================================

        /// <summary>
        /// 检查版本更新，如果有新内容则自动执行完整更新
        /// </summary>
        IEnumerator CheckAndUpdate()
        {
            Debug.Log("检查资源更新...");

            yield return StartCoroutine(GetServerFileList("version"));
            ScanLocalFiles("version");

            bool needUpdate = false;
            yield return StartCoroutine(HasPendingUpdate(v => needUpdate = v));

            if (needUpdate)
            {
                Debug.Log("检测到新版本，自动开始更新...");
                yield return StartCoroutine(UpdateResource());
            }
            else
            {
                Debug.Log("所有资源已是最新版本");
                SetupLuaEnvironment();
            }
        }

        /// <summary> 更新指定子路径下的所有文件 </summary>
        private IEnumerator UpdateChildPath(string childPath)
        {
            Debug.Log($">>> 更新 [{childPath}]");
            yield return StartCoroutine(GetServerFileList(childPath));
            ScanLocalFiles(childPath);
            yield return StartCoroutine(CompareAndDownload(childPath));
        }

        /// <summary> 更新 AB 包资源 </summary>
        private IEnumerator UpdateAB()
        {
            string abChildPath;
            switch (Application.platform)
            {
                case RuntimePlatform.Android:      abChildPath = "AssetBundles_Android"; break;
                case RuntimePlatform.IPhonePlayer: abChildPath = "AssetBundles_IOS";     break;
                default:                           abChildPath = "AssetBundles_Windows"; break;
            }

            Debug.Log($">>> 更新 AB 包 [{abChildPath}]");
            yield return StartCoroutine(GetServerFileList(abChildPath));
            ScanLocalFiles(abChildPath);
            yield return StartCoroutine(DownloadABFiles(abChildPath));
        }

        /// <summary>
        /// 仅对比版本，不下载（返回是否有更新）
        /// </summary>
        private IEnumerator HasPendingUpdate(System.Action<bool> callback)
        {
            // 服务器有但本地没有 / 不一致
            foreach (var sf in serverFiles)
            {
                var lf = localFiles.Find(f => f.name == sf.name);
                if (lf == null || lf.md5 != sf.md5 || lf.size != sf.size)
                {
                    callback(true);
                    yield break;
                }
            }
            // 本地有但服务器已删除
            foreach (var lf in localFiles)
            {
                if (!serverFiles.Exists(f => f.name == lf.name))
                {
                    callback(true);
                    yield break;
                }
            }
            callback(false);
        }

        // ================================================================
        //  服务器文件列表
        // ================================================================

        IEnumerator GetServerFileList(string childPath)
        {
            serverFiles.Clear();

            string apiUrl = $"{url}/api/files?childPath={childPath}";

            using (UnityWebRequest req = UnityWebRequest.Get(apiUrl))
            {
                req.certificateHandler = new CertificateHandlerIgnore();
                yield return req.SendWebRequest();

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

        public IEnumerator CompareAndDownload(string childPath)
        {
            if (serverFiles.Count == 0)
            {
                Debug.Log($"[{childPath}] 服务器文件列表为空，跳过下载");
                yield break;
            }

            int updated = 0;

            // 下载需要更新的文件
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
                    yield return StartCoroutine(DownloadFile(childPath, sf.name));
                    updated++;
                }
                else
                {
                    Debug.Log($"  已最新: {sf.name}");
                }
            }

            // 删除本地多余文件（★★★ 修复：路径补上 childPath ★★★）
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

        IEnumerator DownloadABFiles(string childPath)
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
                    yield return loader.DownloadABAsync(sf.name);
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

        IEnumerator DownloadFile(string childPath, string name)
        {
            string remote = $"{url}/{childPath}/{name}".Replace("\\", "/");
            string local  = Path.Combine(persistentPath, childPath, name);

            // 确保目录存在
            Directory.CreateDirectory(Path.GetDirectoryName(local));

            using (UnityWebRequest req = UnityWebRequest.Get(remote))
            {
                req.certificateHandler = new CertificateHandlerIgnore();
                yield return req.SendWebRequest();

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

        void SetupLuaEnvironment()
        {
            luaEnv.AddLoader(CustomLuaLoader);

            try
            {
                luaEnv.DoString("require('Tile')");
                Debug.Log("Lua 环境初始化完成，Tile.lua 已加载");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Lua 环境初始化（Tile.lua 未找到，可忽略）: {e.Message}");
            }
        }

        private byte[] CustomLuaLoader(ref string filepath)
        {
            string fileName  = filepath.Replace('.', '/') + ".lua";
            string localPath = Path.Combine(persistentPath, "lua", fileName);
            return File.Exists(localPath) ? File.ReadAllBytes(localPath) : null;
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
