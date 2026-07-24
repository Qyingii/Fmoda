using System;
using System.Collections.Generic;

namespace FModa.Modding
{
    /// <summary> 模组运行状态 </summary>
    public enum ModState
    {
        /// <summary> 等待加载 </summary>
        Pending,
        /// <summary> 正在加载 </summary>
        Loading,
        /// <summary> 运行中 </summary>
        Running,
        /// <summary> 加载出错 </summary>
        Error,
        /// <summary> 已卸载 </summary>
        Unloaded,
    }

    /// <summary>
    /// 模组管理接口 —— 提供模组的扫描、加载、查询与生命周期控制。
    ///
    /// 实现类 <see cref="ModdingManager"/> 通过 GF 注册为模块，
    /// 外部代码应通过此接口访问模组功能。
    ///
    /// 使用方式：
    /// <code>
    /// var modding = GF.GetModule&lt;ModdingManager&gt;();
    /// modding.ReloadAll();
    ///
    /// foreach (var mod in modding.GetAllMods())
    ///     Debug.Log($"{mod.id} v{mod.version} — {mod.state}");
    /// </code>
    /// </summary>
    public interface IModding
    {
        // ══════════════════════════════════════════════════════════════
        //  配置
        // ══════════════════════════════════════════════════════════════

        /// <summary> 模组根目录（默认：Application.dataPath + "/../Mods/"）</summary>
        string ModsRootPath { get; set; }

        // ══════════════════════════════════════════════════════════════
        //  运行时状态
        // ══════════════════════════════════════════════════════════════

        /// <summary> 已发现的模组总数 </summary>
        int ModCount { get; }

        // ══════════════════════════════════════════════════════════════
        //  事件
        // ══════════════════════════════════════════════════════════════

        /// <summary> 模组加载完成时触发 </summary>
        event Action<ModInstanceInfo> OnModLoaded;

        /// <summary> 模组卸载时触发 </summary>
        event Action<ModInstanceInfo> OnModUnloaded;

        /// <summary> 模组加载出错时触发（参数：模组 id，错误消息）</summary>
        event Action<string, string> OnModError;

        // ══════════════════════════════════════════════════════════════
        //  公开方法
        // ══════════════════════════════════════════════════════════════

        /// <summary> 重新扫描并加载所有模组（先卸载所有已加载的模组）</summary>
        void ReloadAll();

        /// <summary> 获取指定模组的运行时信息 </summary>
        ModInstanceInfo GetMod(string modId);

        /// <summary> 获取所有已加载模组的运行时信息 </summary>
        IReadOnlyList<ModInstanceInfo> GetAllMods();
    }

    /// <summary>
    /// 模组运行时信息（不可变快照，供外部查询使用）。
    /// </summary>
    public class ModInstanceInfo
    {
        /// <summary> 模组唯一标识 </summary>
        public string id { get; internal set; }
        /// <summary> 显示名称 </summary>
        public string name { get; internal set; }
        /// <summary> 版本号 </summary>
        public string version { get; internal set; }
        /// <summary> 作者 </summary>
        public string author { get; internal set; }
        /// <summary> 描述 </summary>
        public string description { get; internal set; }
        /// <summary> 入口文件路径（相对于模组目录）</summary>
        public string entry { get; internal set; }
        /// <summary> 模组目录的完整路径 </summary>
        public string directory { get; internal set; }
        /// <summary> 当前运行状态 </summary>
        public ModState state { get; internal set; }
    }
}
