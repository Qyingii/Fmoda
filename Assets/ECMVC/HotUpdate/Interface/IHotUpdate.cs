using Cysharp.Threading.Tasks;

namespace ECMVC.HotUpdate
{
    /// <summary>
    /// 热更新管理器接口 —— 定义资源更新的配置、状态与方法契约。
    ///
    /// 实现类 <see cref="HotUpdateManager"/> 以单例形式挂载在场景中，
    /// 外部代码应通过此接口访问热更新功能。
    /// </summary>
    public interface IHotUpdate
    {
        // ── 配置 ──

        /// <summary> 服务器 URL（用于下载资源）</summary>
        string url { get; set; }

        /// <summary> 调试用服务器 URL（编辑器环境默认使用）</summary>
        string debugUrl { get; set; }

        // ── 状态 ──

        /// <summary> 服务器文件总数</summary>
        int ServerFileCount { get; }

        /// <summary> 本地文件总数</summary>
        int LocalFileCount { get; }

        // ── 一键操作（fire-and-forget，适合 UnityEvent 绑定） ──

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

        // ── Lua ──

        /// <summary> 重新初始化 Lua 环境，加载 persistent/lua/ 下所有脚本</summary>
        void SetupLuaEnvironment();

        // ── AB 包 ──

        /// <summary> 加载指定名称的 AB 包，返回包内所有资源</summary>
        /// <param name="abName">AB 包名称</param>
        /// <param name="isLocal">true 从 StreamingAssets 加载，false 从 persistent 加载</param>
        UniTask<object[]> LoadABAsync(string abName, bool isLocal = false);

        /// <summary> 从服务器下载指定名称的 AB 包</summary>
        UniTask DownloadABAsync(string abName);

        // ── 异步 API（程序化调用） ──

        /// <summary> 执行完整资源更新（version + lua + AB）</summary>
        UniTask UpdateResourceAsync();

        /// <summary> 对比并下载指定路径的更新文件</summary>
        UniTask CompareAndDownloadAsync(string childPath);
    }
}
