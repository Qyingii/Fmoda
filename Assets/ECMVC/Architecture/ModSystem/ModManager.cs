using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using XLua;

// Lua 模组系统的核心管理器（XLua 版）。
//
// 使用方式：
//   1. 在场景中新建空 GameObject，挂载 ModManager 脚本
//   2. 运行后自动扫描 Application.dataPath/Mods/ 下的所有子文件夹
//   3. 每个子文件夹为一个 Mod，包含 modInfo.ini 和若干 .lua 文件
//
// Mod 文件夹结构：
//   Mods/
//   └── MyMod/
//       ├── modInfo.ini      （名称/版本/作者/描述，会自动创建默认文件）
//       └── main.lua         （入口，可 require 其他 lua 文件）
//
// modInfo.ini 格式：
//   modName=我的模组
//   version=v1.0.0
//   author=作者名
//   description=简介
//
// Lua 全局变量：
//   Game      → LuaGameAPI 实例（组件注册/日志等）
//   ModsPath  → Mods 文件夹绝对路径字符串
//   Path      → 当前执行 .lua 文件所在文件夹路径（每个文件执行前更新）
//   Vector3   → CS.UnityEngine.Vector3 别名
//   Quaternion→ CS.UnityEngine.Quaternion 别名
//   Color     → CS.UnityEngine.Color 别名
//
// C# 侧将 Lua 组件模板应用到实体：
//   var cc = new CustomComponent(this);
//   AddComponentQ(cc);
//   Game.AddComponent("MyComp", cc);  // 在 Lua 中注册
//   ModManager.Instance.API.ApplyComponent("MyComp", this, cc);  // C# 侧应用
//
// ⚠ 需要先在 Unity 中执行 XLua → Generate Code，并确保以下类型在 LuaCallCSharp 中：
//   LuaGameAPI, UnityEngine.Vector3, UnityEngine.Quaternion, UnityEngine.Color

public class ModManager : MonoBehaviour
{
    public static ModManager Instance { get; private set; }

    private LuaEnv     _luaEnv;
    private string[]   _modFolderPaths = Array.Empty<string>();
    private string     _configPath;

    public LuaGameAPI      API      { get; private set; }
    public string          ModsPath { get; private set; }
    public List<ModInfo>   ModInfos { get; } = new List<ModInfo>();

    // ── Unity 生命周期 ────────────────────────────────────────────────────────

    private void Awake()
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

        Debug.Log("Mod 路径：" + Application.dataPath + "/Mods");
        InitLua();
        LoadAllMods();
    }

    private void OnDestroy()
    {
        _luaEnv?.Dispose();
    }

    // ── XLua 环境初始化 ───────────────────────────────────────────────────────

    private void InitLua()
    {
        ModsPath    = Path.Combine(Application.dataPath, "Mods");
        _configPath = Path.Combine(Application.persistentDataPath, "mods.json");

        if (!Directory.Exists(ModsPath))
            Directory.CreateDirectory(ModsPath);

        _luaEnv = new LuaEnv();
        _luaEnv.AddLoader(CustomModLoader);

        // 注册 API
        API = new LuaGameAPI(_luaEnv);
        _luaEnv.Global.Set("Game", API);
        _luaEnv.Global.Set("ModsPath", ModsPath);

        // 注入 Unity 类型别名，兼容旧版 Lua 脚本的 Vector3(1,2,3) 语法
        _luaEnv.DoString(@"
            Vector3    = CS.UnityEngine.Vector3
            Quaternion = CS.UnityEngine.Quaternion
            Color      = CS.UnityEngine.Color
        ", "ModSystem_Init");
    }

    // ── 自定义 Loader（支持 require）────────────────────────────────────────

    private byte[] CustomModLoader(ref string filepath)
    {
        string fileName = filepath.Replace('.', '/') + ".lua";

        // 优先从当前正在执行的 Mod 文件夹查找
        string currentPath = _luaEnv.Global.Get<string>("Path");
        if (!string.IsNullOrEmpty(currentPath))
        {
            string localPath = Path.Combine(currentPath, fileName);
            if (File.Exists(localPath))
                return File.ReadAllBytes(localPath);
        }

        // 遍历所有 Mod 文件夹
        foreach (var modPath in _modFolderPaths)
        {
            string fullPath = Path.Combine(modPath, fileName);
            if (File.Exists(fullPath))
                return File.ReadAllBytes(fullPath);
        }

        return null;
    }

    // ── Mod 加载 ──────────────────────────────────────────────────────────────

    private void LoadAllMods()
    {
        _modFolderPaths = Directory.GetDirectories(ModsPath)
            .Where(p => !Path.GetFileName(p).StartsWith(".", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (_modFolderPaths.Length == 0) return;

        if (File.Exists(_configPath))
        {
            var setting = JsonUtility.FromJson<ModSetting>(File.ReadAllText(_configPath));

            for (int i = 0; i < _modFolderPaths.Length; i++)
            {
                string path       = _modFolderPaths[i];
                int    cfgIndex   = setting.paths.IndexOf(path);
                bool   shouldLoad = cfgIndex < 0
                                 || cfgIndex >= setting.isLoad.Count
                                 || setting.isLoad[cfgIndex];
                LoadMod(path, shouldLoad);
            }
        }
        else
        {
            foreach (var path in _modFolderPaths)
                LoadMod(path);
        }
    }

    // 加载单个 Mod：读取 modInfo.ini，按文件名顺序执行所有 .lua 文件
    public void LoadMod(string path, bool isLoad = true)
    {
        ParseModInfo(path, isLoad);
        if (!isLoad) return;

        var files = Directory.GetFiles(path, "*.lua", SearchOption.AllDirectories)
            .OrderBy(f => f)
            .ToArray();

        foreach (var file in files)
        {
            _luaEnv.Global.Set("Path", Path.GetDirectoryName(file) + "/");

            try
            {
                string luaCode = File.ReadAllText(file);
                _luaEnv.DoString(luaCode, Path.GetFileName(file));
                Debug.Log($"[Lua] 加载 {file} 成功");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Lua] 执行 {file} 时出错：{e.Message}");
            }
        }
    }

    private void ParseModInfo(string folderPath, bool isLoad)
    {
        var iniPath = Path.Combine(folderPath, "modInfo.ini");
        var info    = new FileInfo(iniPath);

        if (!info.Exists)
        {
            using var sw = info.CreateText();
            sw.WriteLine("modName=未命名模组");
            sw.WriteLine("version=v1.0.0");
            sw.WriteLine("author=未知");
            sw.WriteLine("description=");
        }

        var modInfo = new ModInfo { isInstalled = isLoad };
        using (var sr = info.OpenText())
        {
            string line;
            while ((line = sr.ReadLine()) != null)
            {
                var parts = line.Split('=');
                if (parts.Length < 2) continue;
                switch (parts[0].Trim())
                {
                    case "modName":     modInfo.modName     = parts[1].Trim(); break;
                    case "version":     modInfo.version     = parts[1].Trim(); break;
                    case "author":      modInfo.author      = parts[1].Trim(); break;
                    case "description": modInfo.description = parts[1].Trim(); break;
                }
            }
        }

        ModInfos.Add(modInfo);
    }

    // ── 热重载 ────────────────────────────────────────────────────────────────

    // 卸载所有 Mod 并重新扫描加载
    public void ReloadMods()
    {
        ModInfos.Clear();
        API.ClearComponents();

        // 销毁旧 LuaEnv，重新创建（确保 Lua 状态完全重置）
        _luaEnv.Dispose();
        InitLua();
        LoadAllMods();

        Debug.Log("[ModManager] 热重载完成");
    }

    // ── 设置持久化 ────────────────────────────────────────────────────────────

    public void SaveSettings()
    {
        var setting = new ModSetting { paths = _modFolderPaths.ToList() };
        foreach (var modInfo in ModInfos)
            setting.isLoad.Add(modInfo.isInstalled);

        File.WriteAllText(_configPath, JsonUtility.ToJson(setting, true));
    }
}
