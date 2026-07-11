using System;
using System.IO;
using UnityEngine;
using XLua;
using Cysharp.Threading.Tasks;

namespace ECMVC.Hotfix
{
    [Hotfix]
    public class HotfixManager : MonoBehaviour, IHotfix
    {
        public static HotfixManager Instance;

        [SerializeField]
        private string url = @"https://frp-gap.com:64849";

        [SerializeField]
        private string debugUrl = @"https://localhost:6567";

        private ResourcesDownload resourcesDownload;
        private LuaLoad luaLoad;

        // ── 接口状态 ──
        private UpdateState _state = UpdateState.Idle;
        private string _lastError;
        private VersionData _remoteVersion;
        private VersionData _localVersion;

        // ── 事件 ──
        public event Action<VersionData, VersionData> OnVersionChecked;
        public event Action OnHotfixStarted;
        public event Action OnHotfixComplete;
        public event Action OnLuaEnvironmentReady;
        public event Action<string> OnUpdateError;
        public event Action<UpdateState> OnStateChanged;

        // ══════════════════════════════════════════════════════════════
        //  属性
        // ══════════════════════════════════════════════════════════════

        public string ServerUrl
        {
            get => resourcesDownload?.ServerUrl ?? url;
            set
            {
                url = value;
                if (resourcesDownload != null)
                    resourcesDownload.ServerUrl = value;
            }
        }

        public UpdateState State
        {
            get => _state;
            private set
            {
                if (_state == value) return;
                _state = value;
                OnStateChanged?.Invoke(_state);
            }
        }

        public string LastError              => _lastError;
        public int ServerFileCount           => resourcesDownload?.ServerFileCount ?? 0;
        public int LocalFileCount            => resourcesDownload?.LocalFileCount  ?? 0;
        public VersionData RemoteVersion     => _remoteVersion;
        public VersionData LocalVersion      => _localVersion;
        public bool HasNewAppVersion         => _remoteVersion != null
                                                && (_localVersion == null || _remoteVersion.version != _localVersion.version);
        public bool HasNewHotfix             => _remoteVersion != null
                                                && _remoteVersion.hotfixVersion != (_localVersion?.hotfixVersion ?? "");
        public string AppDownloadUrl         => _remoteVersion?.downloadUrl;

        string IHotfix.url                   { get => resourcesDownload?.ServerUrl ?? url; set => ServerUrl = value; }
        string IHotfix.debugUrl              { get => debugUrl; set => debugUrl = value; }

        // ══════════════════════════════════════════════════════════════
        //  Unity 生命周期
        // ══════════════════════════════════════════════════════════════

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

            string persistentPath = Application.persistentDataPath;
            if (!Directory.Exists(persistentPath))
                Directory.CreateDirectory(persistentPath);

            resourcesDownload = new ResourcesDownload(persistentPath, Application.isEditor, url, debugUrl);
            luaLoad           = new LuaLoad(persistentPath);
        }

        void Start()
        {
            CheckAndUpdateAsync().Forget();
        }

        void OnDestroy()
        {
            resourcesDownload?.Cancel();
            luaLoad?.Dispose();
        }

        // ══════════════════════════════════════════════════════════════
        //  公开方法（一键操作）
        // ══════════════════════════════════════════════════════════════

        public void StartCheckUpdate()               => CheckAndUpdateAsync().Forget();
        public void StartFullUpdate()                => UpdateResourceAsync().Forget();
        public void StartGetFileList(string path)    => resourcesDownload?.GetServerFileListAsync(path).Forget();
        public void StartScanLocal(string childPath) => resourcesDownload?.ScanLocalFiles(childPath);
        public void StartCompareDownload(string childPath)
            => resourcesDownload?.CompareAndDownloadAsync(childPath).Forget();

        // ══════════════════════════════════════════════════════════════
        //  完整热更新流程（版本检查 → 按需下载 → 加载）
        // ══════════════════════════════════════════════════════════════

        public async UniTask UpdateResourceAsync()
        {
            await UniTask.Yield();
            Debug.Log("=== XLua 资源更新开始 ===");

            await resourcesDownload.UpdateChildPathAsync("version");
            await resourcesDownload.UpdateChildPathAsync("lua");

            State = UpdateState.LoadingLua;
            luaLoad.SetupLuaEnvironment();
            OnLuaEnvironmentReady?.Invoke();

            await resourcesDownload.UpdateABAsync();

            Debug.Log("=== XLua 资源更新完成 ===");
        }

        private async UniTask DownloadHotfixResourcesAsync()
        {
            State = UpdateState.Downloading;
            OnHotfixStarted?.Invoke();

            // 先保存最新版本信息（标记热更已应用）
            resourcesDownload.SaveLocalVersion(_remoteVersion);
            _localVersion = _remoteVersion;

            await resourcesDownload.UpdateChildPathAsync("lua");
            await resourcesDownload.UpdateABAsync();

            OnHotfixComplete?.Invoke();
        }

        // ══════════════════════════════════════════════════════════════
        //  版本驱动的检查流程
        // ══════════════════════════════════════════════════════════════

        private async UniTask CheckAndUpdateAsync()
        {
            State = UpdateState.Checking;
            _lastError = null;

            try
            {
                Debug.Log("检查资源更新...");

                // ── 1) 获取远程版本 ──
                _remoteVersion = await resourcesDownload.GetRemoteVersionAsync();
                if (_remoteVersion == null)
                {
                    _lastError = "无法获取远程版本信息";
                    Debug.LogWarning(_lastError);
                    FireError(_lastError);

                    State = UpdateState.LoadingLua;
                    luaLoad.SetupLuaEnvironment();
                    OnLuaEnvironmentReady?.Invoke();
                    return;
                }

                // ── 2) 读取本地版本 ──
                _localVersion = resourcesDownload.GetLocalVersion();

                // ── 3) 触发版本检查事件（UI 可在此处响应） ──
                OnVersionChecked?.Invoke(_remoteVersion, _localVersion);

                // ── 4) 主版本号比对 ──
                if (HasNewAppVersion)
                {
                    Debug.Log($"📢 发现新版本 {_remoteVersion.version}" +
                              $"（当前: {_localVersion?.version ?? "无"}）");
                    if (!string.IsNullOrEmpty(_remoteVersion.downloadUrl))
                        Debug.Log($"📥 下载地址: {_remoteVersion.downloadUrl}");
                    if (!string.IsNullOrEmpty(_remoteVersion.updateContent))
                        Debug.Log($"📝 更新内容: {_remoteVersion.updateContent}");
                }

                // ── 5) 热更版本号比对 ──
                if (HasNewHotfix)
                {
                    Debug.Log($"🔥 检测到热更新 {_remoteVersion.hotfixVersion}" +
                              $"（当前: {_localVersion?.hotfixVersion ?? "无"}）");
                    if (!string.IsNullOrEmpty(_remoteVersion.hotfixUpdateContent))
                        Debug.Log($"📝 热更说明: {_remoteVersion.hotfixUpdateContent}");

                    await DownloadHotfixResourcesAsync();
                    Debug.Log("✅ 热更新资源下载完成");
                }
                else
                {
                    Debug.Log("✓ 热更新已是最新版本");
                }

                // ── 6) 加载 Lua 环境 ──
                State = UpdateState.LoadingLua;
                luaLoad.SetupLuaEnvironment();
                OnLuaEnvironmentReady?.Invoke();

                State = UpdateState.Complete;
            }
            catch (System.Exception e)
            {
                _lastError = e.Message;
                Debug.LogError($"[XLuaManager] 检查更新失败: {e.Message}");
                FireError(_lastError);

                // 兜底：仍然尝试加载本地 Lua
                State = UpdateState.LoadingLua;
                luaLoad.SetupLuaEnvironment();
                OnLuaEnvironmentReady?.Invoke();
            }
        }

        private void FireError(string msg)
        {
            OnUpdateError?.Invoke(msg);
        }

        // ══════════════════════════════════════════════════════════════
        //  IHotfix 接口实现 —— 其余委托到服务层
        // ══════════════════════════════════════════════════════════════

        public void SetupLuaEnvironment() => luaLoad.SetupLuaEnvironment();
        public async UniTask<object[]> LoadABAsync(string abName, bool isLocal = false)
            => await luaLoad.LoadABAsync(abName, isLocal);

        public async UniTask DownloadABAsync(string abName)
            => await resourcesDownload.DownloadABAsync(abName);

        public async UniTask CompareAndDownloadAsync(string childPath)
            => await resourcesDownload.CompareAndDownloadAsync(childPath);
    }
}
