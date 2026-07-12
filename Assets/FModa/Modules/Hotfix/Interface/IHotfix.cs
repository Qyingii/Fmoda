using System;
using Cysharp.Threading.Tasks;

namespace FModa.Hotfix
{
    /// <summary> 热更新状态枚举 </summary>
    public enum UpdateState
    {
        /// <summary> 空闲 / 未开始 </summary>
        Idle,
        /// <summary> 正在检查远程版本 </summary>
        Checking,
        /// <summary> 正在下载热更新资源（lua / AB）</summary>
        Downloading,
        /// <summary> 正在加载 Lua 环境 </summary>
        LoadingLua,
        /// <summary> 更新完成 </summary>
        Complete,
        /// <summary> 更新出错 </summary>
        Error,
    }

    /// <summary>
    /// 热更新管理器接口 —— 定义资源更新的配置、状态与方法契约。
    ///
    /// 实现类 <see cref="HotfixManager"/> 以单例形式挂载在场景中，
    /// 外部代码应通过此接口访问热更新功能。
    /// </summary>
    public interface IHotfix
    {
        // ══════════════════════════════════════════════════════════════
        //  配置
        // ══════════════════════════════════════════════════════════════

        /// <summary> 服务器 URL（用于下载资源）</summary>
        string url { get; set; }

        /// <summary> 调试用服务器 URL（编辑器环境默认使用）</summary>
        string debugUrl { get; set; }

        // ══════════════════════════════════════════════════════════════
        //  运行时状态
        // ══════════════════════════════════════════════════════════════

        /// <summary> 当前更新状态 </summary>
        UpdateState State { get; }

        /// <summary> 上一次的错误消息（无错误时为 null）</summary>
        string LastError { get; }

        /// <summary> 服务器文件总数</summary>
        int ServerFileCount { get; }

        /// <summary> 本地文件总数</summary>
        int LocalFileCount { get; }

        // ══════════════════════════════════════════════════════════════
        //  版本信息（版本检查后可用）
        // ══════════════════════════════════════════════════════════════

        /// <summary> 远程 version.json 解析结果（检查完成后有值）</summary>
        VersionData RemoteVersion { get; }

        /// <summary> 本地 version.json 解析结果</summary>
        VersionData LocalVersion { get; }

        /// <summary> 主版本是否有更新（remote.version != local.version）</summary>
        bool HasNewAppVersion { get; }

        /// <summary> 热更版本是否有更新（remote.hotfixVersion != local.hotfixVersion）</summary>
        bool HasNewHotfix { get; }

        /// <summary> 新版本下载地址（RemoteVersion?.downloadUrl 的快捷方式）</summary>
        string AppDownloadUrl { get; }

        // ══════════════════════════════════════════════════════════════
        //  事件（供 UI / 其他系统订阅）
        // ══════════════════════════════════════════════════════════════

        /// <summary> 版本检查完成时触发（参数：远程版本，本地版本）</summary>
        event Action<VersionData, VersionData> OnVersionChecked;

        /// <summary> 开始下载热更新资源时触发</summary>
        event Action OnHotfixStarted;

        /// <summary> 热更新资源下载完成时触发</summary>
        event Action OnHotfixComplete;

        /// <summary> Lua 环境就绪时触发</summary>
        event Action OnLuaEnvironmentReady;

        /// <summary> 更新流程出错时触发（参数：错误消息）</summary>
        event Action<string> OnUpdateError;

        /// <summary> 状态变化时触发</summary>
        event Action<UpdateState> OnStateChanged;

        // ══════════════════════════════════════════════════════════════
        //  一键操作（fire-and-forget，适合 UnityEvent 绑定）
        // ══════════════════════════════════════════════════════════════

        /// <summary> 检查资源是否有更新</summary>
        void StartCheckUpdate();

        /// <summary> 执行完整资源更新（version + lua + AB）</summary>
        void StartFullUpdate();

        /// <summary> 获取服务器指定路径的文件列表</summary>
        void StartGetFileList(string path);

        /// <summary> 扫描本地指定路径的文件</summary>
        void StartScanLocal(string childPath);

        /// <summary> 对比并下载指定路径的更新文件</summary>
        void StartCompareDownload(string childPath);

        // ══════════════════════════════════════════════════════════════
        //  Lua
        // ══════════════════════════════════════════════════════════════

        /// <summary> 重新初始化 Lua 环境，加载 persistent/lua/ 下所有脚本</summary>
        void SetupLuaEnvironment();

        // ══════════════════════════════════════════════════════════════
        //  AB 包
        // ══════════════════════════════════════════════════════════════

        /// <summary> 加载指定名称的 AB 包，返回包内所有资源</summary>
        UniTask<object[]> LoadABAsync(string abName, bool isLocal = false);

        /// <summary> 从服务器下载指定名称的 AB 包</summary>
        UniTask DownloadABAsync(string abName);

        // ══════════════════════════════════════════════════════════════
        //  异步 API
        // ══════════════════════════════════════════════════════════════

        /// <summary> 执行完整资源更新（version + lua + AB）</summary>
        UniTask UpdateResourceAsync();

        /// <summary> 对比并下载指定路径的更新文件</summary>
        UniTask CompareAndDownloadAsync(string childPath);
    }
}
