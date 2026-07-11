using System.IO;
using UnityEngine;
using XLua;
using Cysharp.Threading.Tasks;

namespace ECMVC.Hotfix
{
    /// <summary>
    /// Lua 环境管理器 —— 负责 LuaEnv 生命周期、Lua 脚本加载以及 AB 包加载。
    /// 纯逻辑类，不依赖 MonoBehaviour，由 <see cref="HotfixManager"/> 创建并驱动。
    /// </summary>
    public class LuaLoad
    {
        private LuaEnv luaEnv;
        private string persistentPath;

        // ── 构造 / 析构 ──

        public LuaLoad(string persistentPath)
        {
            this.persistentPath = persistentPath;
            luaEnv              = new LuaEnv();
        }

        /// <summary> 释放 Lua 虚拟机 </summary>
        public void Dispose()
        {
            if (luaEnv == null) return;
            try { luaEnv.Dispose(); }
            catch (System.InvalidOperationException)
            {
                // 场景卸载时 hotfix 桥接尚有残留（WeakReference 未回收），
                // 不影响进程退出后的内存回收，忽略即可。
            }
            luaEnv = null;
        }

        // ================================================================
        //  Lua 环境初始化
        // ================================================================

        /// <summary>
        /// 初始化 Lua 环境：注册自定义加载器（将 require('foo.bar') 映射到
        /// persistent/lua/foo/bar.lua），然后加载 main.lua 入口文件。
        /// 其他 Lua 脚本由 main.lua 自行 require 加载。
        /// </summary>
        public void SetupLuaEnvironment()
        {
            // ── 自定义加载器：将 require('foo.bar') 映射到 persistent/lua/foo/bar.lua ──
            luaEnv.AddLoader((ref string filepath) =>
            {
                string fileName  = filepath.Replace('.', '/') + ".lua";
                string localPath = Path.Combine(persistentPath, "lua", fileName);
                return File.Exists(localPath) ? File.ReadAllBytes(localPath) : null;
            });

            // ── 只加载入口 main.lua，其他脚本由 lua 内部 require ──
            string mainPath = Path.Combine(persistentPath, "lua", "main.lua");
            if (!File.Exists(mainPath))
            {
                Debug.LogWarning($"Lua 入口文件不存在，跳过加载: {mainPath}");
                return;
            }

            try
            {
                luaEnv.DoString("require('main')");
                Debug.Log("[Lua] main.lua 加载成功");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Lua] main.lua 加载失败: {e.Message}");
            }
        }

        // ================================================================
        //  AB 包加载
        // ================================================================

        /// <summary> 加载指定名称的 AB 包，返回包内所有资源（失败返回 null）</summary>
        /// <param name="abName">AB 包名称</param>
        /// <param name="isLocal">true 从 StreamingAssets 加载，false 从 persistent 加载</param>
        public async UniTask<object[]> LoadABAsync(string abName, bool isLocal = false)
        {
            var loader = new LoadAB("");
            return await loader.LoadABAsync(abName, false, isLocal);
        }
    }
}
