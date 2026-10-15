using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using XLua;

namespace FModa.Modding
{
    /// <summary>
    /// 模组管理器 —— 负责扫描、加载/卸载 XLua 模组，调度生命周期。
    ///
    /// 遵循模块规范：
    ///   - 实现 <see cref="IModding"/> 供外部访问
    ///   - 实现 <see cref="IModule"/> 通过 GF 接收生命周期派发
    ///
    /// 使用方式：
    /// <code>
    /// // 首次访问自动创建（通过 GF 的模块自动创建机制）
    /// // 注意只能用具体类型取：GetModule&lt;T&gt; 的约束是 where T : class, IModule，
    /// // 而 IModding 不继承 IModule，写 GF.GetModule&lt;IModding&gt;() 编译不过
    /// var modding = GF.GetModule&lt;ModdingManager&gt;();
    ///
    /// // 手动重新加载所有模组
    /// modding.ReloadAll();
    /// </code>
    ///
    /// 模组目录结构：
    /// <code>
    /// Mods/
    /// └── MyMod/
    ///     ├── mod.json          ← 清单（必选）
    ///     └── main.lua          ← 入口脚本（由 mod.json.entry 指定）
    /// </code>
    ///
    /// main.lua 应返回一个 table，包含以下可选生命周期方法：
    /// <code>
    /// local M = {}
    /// function M.on_start(api)    end  -- 模组启动时调用
    /// function M.on_update(api)   end  -- 每逻辑帧调用
    /// function M.on_destroy(api)  end  -- 模组卸载时调用
    /// return M
    /// </code>
    /// </summary>
    [Hotfix]
    public class ModdingManager : IModding, IModule
    {
        public Type InterfaceType => typeof(IModding);
        public int Priority { get; } = 10;

        // ══════════════════════════════════════════════════════════════
        //  配置
        // ══════════════════════════════════════════════════════════════

        /// <summary> 模组根目录（默认取项目/构建目录下的 Mods/ 文件夹）</summary>
        public string ModsRootPath { get; set; }

        // ══════════════════════════════════════════════════════════════
        //  内部状态
        // ══════════════════════════════════════════════════════════════

        /// <summary> id → ModInstance（运行时实例）</summary>
        private readonly Dictionary<string, ModInstance> _mods = new();

        // ══════════════════════════════════════════════════════════════
        //  IModding 事件
        // ══════════════════════════════════════════════════════════════

        public event Action<ModInstanceInfo> OnModLoaded;
        public event Action<ModInstanceInfo> OnModUnloaded;
        public event Action<string, string> OnModError;

        // ══════════════════════════════════════════════════════════════
        //  IModding 属性
        // ══════════════════════════════════════════════════════════════

        public int ModCount => _mods.Count;

        // ══════════════════════════════════════════════════════════════
        //  IModule 生命周期
        // ══════════════════════════════════════════════════════════════

        public void OnAwake()
        {
            // 默认模组根目录：项目/构建目录下的 Mods/
            if (string.IsNullOrEmpty(ModsRootPath))
            {
                ModsRootPath = Path.GetFullPath(Path.Combine(
                    Application.dataPath, "..", "Mods"
                ));
            }

            Debug.Log($"[Modding] 模组根目录: {ModsRootPath}");
        }

        public void OnStart()
        {
            // 首次自动扫描加载
            ScanAndLoadMods();
        }

        public void OnUpdate()
        {
            // 逐帧派发到模组
            foreach (var kv in _mods)
            {
                kv.Value.Update();
            }
        }

        public void OnDestroy()
        {
            UnloadAllMods();
        }

        // 无需实现
        public void OnLateUpdate() { }
        public void OnApplicationQuit() { }

        // ══════════════════════════════════════════════════════════════
        //  IModding 方法
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// 重新扫描并加载所有模组。
        /// 会先卸载所有已加载的模组，再从磁盘扫描加载。
        /// </summary>
        public void ReloadAll()
        {
            UnloadAllMods();
            ScanAndLoadMods();
        }

        /// <summary> 获取指定模组的运行时信息 </summary>
        public ModInstanceInfo GetMod(string modId)
        {
            if (_mods.TryGetValue(modId, out var inst))
                return inst.ToInfo();
            return null;
        }

        /// <summary> 获取所有已加载模组的运行时信息（快照列表）</summary>
        public IReadOnlyList<ModInstanceInfo> GetAllMods()
        {
            var list = new List<ModInstanceInfo>(_mods.Count);
            foreach (var kv in _mods)
                list.Add(kv.Value.ToInfo());
            return list;
        }

        // ══════════════════════════════════════════════════════════════
        //  内部 — 扫描与加载
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// 扫描 Mods 目录，加载每个子目录中的模组。
        /// </summary>
        private void ScanAndLoadMods()
        {
            if (!Directory.Exists(ModsRootPath))
            {
                Debug.LogWarning($"[Modding] 模组目录不存在: {ModsRootPath}");
                return;
            }

            var dirs = Directory.GetDirectories(ModsRootPath);
            Debug.Log($"[Modding] 扫描到 {dirs.Length} 个模组目录");

            foreach (var dir in dirs)
            {
                LoadModFromDirectory(dir);
            }

            Debug.Log($"[Modding] 成功加载 {_mods.Count} 个模组");
        }

        /// <summary>
        /// 从单个目录加载模组。
        /// 预期目录结构：
        ///   MyMod/
        ///   ├── mod.json
        ///   └── main.lua（由 manifest.entry 指定）
        /// </summary>
        private void LoadModFromDirectory(string dirPath)
        {
            var manifestPath = Path.Combine(dirPath, "mod.json");

            if (!File.Exists(manifestPath))
            {
                Debug.LogWarning($"[Modding] 跳过 {Path.GetFileName(dirPath)}：缺少 mod.json");
                return;
            }

            // ── 解析清单 ──
            ModManifest manifest;
            try
            {
                var json = File.ReadAllText(manifestPath);
                manifest = JsonUtility.FromJson<ModManifest>(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Modding] {Path.GetFileName(dirPath)} mod.json 解析失败: {e.Message}");
                return;
            }

            if (manifest == null || !manifest.IsValid())
            {
                Debug.LogError($"[Modding] {Path.GetFileName(dirPath)} mod.json 缺少必要字段 (id/name/entry)");
                return;
            }

            // ── 已存在同名模组则跳过 ──
            if (_mods.ContainsKey(manifest.id))
            {
                Debug.LogWarning($"[Modding] 模组 {manifest.id} 已加载，跳过");
                return;
            }

            // ── 创建实例并加载 ──
            var instance = new ModInstance(manifest, dirPath);
            _mods[manifest.id] = instance;

            instance.Load();

            // ── 通知外部 ──
            var info = instance.ToInfo();
            if (instance.State == FModa.Modding.ModState.Running)
            {
                OnModLoaded?.Invoke(info);
            }
            else
            {
                OnModError?.Invoke(manifest.id, $"加载失败，状态: {instance.State}");
            }
        }

        /// <summary> 卸载所有模组 </summary>
        private void UnloadAllMods()
        {
            foreach (var kv in _mods)
            {
                kv.Value.Dispose();
                OnModUnloaded?.Invoke(kv.Value.ToInfo());
            }
            _mods.Clear();
        }

        // ══════════════════════════════════════════════════════════════
        //  内部 — 模组实例（每个模组一个）
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// 单个模组的运行时实例。
        /// 每个模组拥有独立的 LuaEnv，实现数据完全隔离。
        /// </summary>
        private class ModInstance : IDisposable
        {
            public ModManifest Manifest { get; }
            public string DirectoryPath { get; }
            public ModState State => _state;

            private ModState _state = FModa.Modding.ModState.Pending;
            private LuaEnv _luaEnv;
            private LuaTable _modTable;
            private ModAPI _api;

            public ModInstance(ModManifest manifest, string dirPath)
            {
                Manifest = manifest;
                DirectoryPath = dirPath;
            }

            /// <summary>
            /// 加载模组：创建 LuaEnv → 加载入口脚本 → 调用 on_start。
            /// </summary>
            public void Load()
            {
                _state = FModa.Modding.ModState.Loading;

                try
                {
                    _luaEnv = new LuaEnv();

                    // 模组专属 ModAPI（每个模组独立实例）
                    _api = new ModAPI(Manifest);

                    // 注册为全局，Lua 侧通过 api 参数接收
                    _luaEnv.Global["MOD_API"] = _api;

                    // ── 添加自定义加载器 ──
                    // 让 require('xxx') 从模组目录查找 xxx.lua
                    _luaEnv.AddLoader(ResolveLuaFile);

                    // ── 加载入口脚本 ──
                    var entryName = Path.GetFileNameWithoutExtension(Manifest.entry);
                    var entryPath = Path.Combine(DirectoryPath, Manifest.entry);

                    if (!File.Exists(entryPath))
                    {
                        Debug.LogError($"[Modding] 模组 {Manifest.id} 入口文件不存在: {entryPath}");
                        _state = FModa.Modding.ModState.Error;
                        Cleanup();
                        return;
                    }

                    // 执行并捕获返回的 table
                    var results = _luaEnv.DoString($"return require('{entryName}')");
                    if (results != null && results.Length > 0)
                        _modTable = results[0] as LuaTable;

                    if (_modTable == null)
                    {
                        Debug.LogError($"[Modding] 模组 {Manifest.id} 入口脚本未返回 table");
                        _state = FModa.Modding.ModState.Error;
                        Cleanup();
                        return;
                    }

                    // ── 调用 on_start ──
                    CallModFunction("on_start", _api);

                    // 清除全局（避免泄漏）
                    _luaEnv.Global["MOD_API"] = null;

                    _state = FModa.Modding.ModState.Running;
                    Debug.Log($"[Modding] ✔ 模组 {Manifest.id} v{Manifest.version} 已加载");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Modding] 模组 {Manifest.id} 加载异常: {e}");
                    _state = FModa.Modding.ModState.Error;
                    Cleanup();
                }
            }

            /// <summary>
            /// 每帧更新：调用模组的 on_update。
            /// GF 使用的是固定 tick 驱动，这里直接每帧调用即可。
            /// </summary>
            public void Update()
            {
                if (_state != FModa.Modding.ModState.Running || _modTable == null)
                    return;

                CallModFunction("on_update", _api);
            }

            public void Dispose()
            {
                if (_state == FModa.Modding.ModState.Running || _state == FModa.Modding.ModState.Loading)
                {
                    CallModFunction("on_destroy", _api);
                }
                Cleanup();
                _state = FModa.Modding.ModState.Unloaded;
                Debug.Log($"[Modding] 模组 {Manifest.id} 已卸载");
            }

            /// <summary> 生成不可变运行时信息快照 </summary>
            public ModInstanceInfo ToInfo()
            {
                return new ModInstanceInfo
                {
                    id = Manifest.id,
                    name = Manifest.name,
                    version = Manifest.version,
                    author = Manifest.author,
                    description = Manifest.description,
                    entry = Manifest.entry,
                    directory = DirectoryPath,
                    state = _state,
                };
            }

            // ── 内部 ──

            /// <summary>
            /// XLua 自定义加载器：将 require 路径映射到模组目录下的 .lua 文件。
            /// </summary>
            private byte[] ResolveLuaFile(ref string filepath)
            {
                var path = Path.Combine(DirectoryPath, filepath.Replace('.', Path.DirectorySeparatorChar) + ".lua");
                return File.Exists(path) ? File.ReadAllBytes(path) : null;
            }

            /// <summary>
            /// 安全调用模组 table 中的指定函数。
            /// </summary>
            private void CallModFunction(string funcName, params object[] args)
            {
                if (_modTable == null) return;

                try
                {
                    var fn = _modTable.Get<LuaFunction>(funcName);
                    fn?.Call(args);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Modding] 模组 {Manifest.id}.{funcName} 异常: {e}");
                }
            }

            private void Cleanup()
            {
                _modTable?.Dispose();
                _modTable = null;
                _luaEnv?.Dispose();
                _luaEnv = null;
                _api = null;
            }
        }

    }
}
