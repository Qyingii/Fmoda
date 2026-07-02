using System.Collections.Generic;
using System.IO;
using System.Linq;
using MoonSharp.Interpreter;
using UnityEngine;

// Lua 模组系统的核心管理器。
//
// 使用方式：
//   1. 在场景中新建空 GameObject，挂载 LuaManager 脚本
//   2. 运行后自动扫描 Application.dataPath/Mods/ 下的所有子文件夹
//   3. 每个子文件夹为一个 Mod，包含 modInfo.ini 和若干 .lua 文件
//
// Mod 文件夹结构：
//   Mods/
//   └── MyMod/
//       ├── modInfo.ini      （名称/版本/作者/描述）
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
//   Vector3   → LuaVector3 类型
//   Quaternion→ LuaQuaternion 类型
//   Color     → LuaColor 类型
//
// C# 侧将 Lua 组件模板应用到实体：
//   var cc = new CustomComponent(this);
//   AddComponentQ(cc);
//   LuaManager.Instance.API.ApplyComponent("MyComp", cc);

public class LuaManager : MonoBehaviour
{
    public static LuaManager Instance { get; private set; }

    private Script     _script;
    private string[]   _modFolderPaths = System.Array.Empty<string>();
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
            return; // 中止重复实例的初始化，Instance.API 由首个实例负责
        }
        
        Debug.Log("mod路径："+Application.dataPath+"/Mods");
        RegisterTypes();
        InitScript();
        LoadAllMods();
    }

    // ── 初始化 ────────────────────────────────────────────────────────────────

    private void RegisterTypes()
    {
        UserData.RegisterType<LuaVector3>();
        UserData.RegisterType<LuaQuaternion>();
        UserData.RegisterType<LuaColor>();
        UserData.RegisterType<LuaGameAPI>();
    }

    private void InitScript()
    {
        ModsPath    = Path.Combine(Application.dataPath, "Mods");
        _configPath = Path.Combine(Application.persistentDataPath, "mods.json");

        if (!Directory.Exists(ModsPath))
            Directory.CreateDirectory(ModsPath);

        _script = new Script();
        _script.Options.ScriptLoader = new LuaFileLoader();
        _script.Options.DebugPrint   = s => Debug.Log($"[Lua] {s}");

        API = new LuaGameAPI(_script);
        ExposeGlobals();
    }

    private void ExposeGlobals()
    {
        _script.Globals["Game"]       = API;
        _script.Globals["ModsPath"]   = ModsPath;
        _script.Globals["Vector3"]    = typeof(LuaVector3);
        _script.Globals["Quaternion"] = typeof(LuaQuaternion);
        _script.Globals["Color"]      = typeof(LuaColor);
    }

    // ── Mod 加载 ──────────────────────────────────────────────────────────────

    private void LoadAllMods()
    {
        _modFolderPaths = Directory.GetDirectories(ModsPath)
            .Where(p => !Path.GetFileName(p).StartsWith(".", System.StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (_modFolderPaths.Length == 0) return;

        if (File.Exists(_configPath))
        {
            var setting = JsonUtility.FromJson<ModSetting>(File.ReadAllText(_configPath));

            for (int i = 0; i < _modFolderPaths.Length; i++)
            {
                string path       = _modFolderPaths[i];
                int    cfgIndex   = setting.paths.IndexOf(path);
                bool   shouldLoad = cfgIndex < 0 || cfgIndex >= setting.isLoad.Count || setting.isLoad[cfgIndex];
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
            _script.Globals["Path"] = Path.GetDirectoryName(file) + "/";

            try
            {
                Debug.Log($"[Lua] 加载 {file}成功");
                _script.DoFile(file);
            }
            catch (System.Exception e)
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
        InitScript();
        LoadAllMods();
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
