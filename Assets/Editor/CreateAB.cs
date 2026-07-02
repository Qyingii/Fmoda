using System.IO;
using UnityEditor;
using UnityEngine;

// │╱ 序列化数据结构 ╱─────────────────────────────────────────────────────────

[System.Serializable]
public class ABBuildConfig
{
    public string version       = "v1.0.0";
    public string updateContent = "";
}

[System.Serializable]
public class VersionData
{
    public string version;
    public string updateContent;
}

// │╱ 主窗口 ╱─────────────────────────────────────────────────────────────────

public class CreateAB : EditorWindow
{
    // ── AB 包平台选项 ──
    private bool buildAndroid = true;
    private bool buildWindows = true;
    private bool buildIOS;

    // ── Version 配置 ──
    private string versionNumber   = "v1.0.0";
    private string updateContent   = "";
    private bool   showVersion     = true;

    // ── 持久化路径 ──
    private static string ConfigDir =>
        Path.Combine(Application.dataPath, "Editor", "EditorConfig");

    private static string ConfigFilePath =>
        Path.Combine(ConfigDir, "ab_config.json");

    // ══════════════════════════════════════════════════════════════════════
    //  菜单入口
    // ══════════════════════════════════════════════════════════════════════

    [MenuItem("Assets/AB包/打开打包窗口")]
    private static void ShowWindow()
    {
        var w = GetWindow<CreateAB>("AB包构建工具");
        w.minSize = new Vector2(420, 300);
        w.LoadFromDisk();
        w.Show();
    }

    // ══════════════════════════════════════════════════════════════════════
    //  窗口生命周期
    // ══════════════════════════════════════════════════════════════════════

    private void OnEnable()  => LoadFromDisk();
    private void OnDisable() => SaveToDisk();

    // ══════════════════════════════════════════════════════════════════════
    //  GUI
    // ══════════════════════════════════════════════════════════════════════

    private void OnGUI()
    {
        DrawHeader();
        EditorGUILayout.Space(4);
        DrawABSection();
        EditorGUILayout.Space(4);
        DrawVersionSection();
        EditorGUILayout.Space(4);
        DrawLuaHint();
        EditorGUILayout.Space(4);
        DrawFooter();
    }

    private void DrawHeader()
    {
        EditorGUILayout.Space(6);
        var title = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
        EditorGUILayout.LabelField("🛠 AB 包构建工具", title);
        EditorGUILayout.Space(2);
    }

    // ── AB 包 ─────────────────────────────────────────────────────────────

    private void DrawABSection()
    {
        EditorGUILayout.LabelField("📦 AB 包（AssetBundle）", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        buildAndroid = EditorGUILayout.ToggleLeft("Android", buildAndroid);
        buildWindows = EditorGUILayout.ToggleLeft("Windows", buildWindows);
        buildIOS     = EditorGUILayout.ToggleLeft("iOS", buildIOS);
        EditorGUI.indentLevel--;
    }

    // ── Version ───────────────────────────────────────────────────────────

    private void DrawVersionSection()
    {
        showVersion = EditorGUILayout.BeginFoldoutHeaderGroup(showVersion, "📋 Version（版本信息）");
        if (showVersion)
        {
            EditorGUI.indentLevel++;
            versionNumber = EditorGUILayout.TextField("版本号", versionNumber);
            EditorGUILayout.LabelField("更新说明");
            updateContent = EditorGUILayout.TextArea(updateContent, GUILayout.MinHeight(40));
            EditorGUI.indentLevel--;

            EditorGUILayout.HelpBox(
                "→ 生成 StreamingAssets/version/version.json\n" +
                "{ \"version\": \"...\", \"updateContent\": \"...\" }",
                MessageType.Info);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    // ── Lua ───────────────────────────────────────────────────────────────

    private void DrawLuaHint()
    {
        EditorGUILayout.LabelField("📜 Lua（热更新脚本）", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Lua 文件请直接用 VS Code 编辑 StreamingAssets/lua/ 目录\n" +
            "打包时自动创建 lua 文件夹，已有文件不会被清除",
            MessageType.Info);
    }

    // ── 底部 ──────────────────────────────────────────────────────────────

    private void DrawFooter()
    {
        int selected = (buildAndroid ? 1 : 0) + (buildWindows ? 1 : 0) + (buildIOS ? 1 : 0);
        EditorGUILayout.HelpBox(
            selected == 0 ? "请至少选择一个平台" : $"已选择 {selected} 个平台",
            selected == 0 ? MessageType.Warning : MessageType.Info);

        if (GUILayout.Button("🚀 开始打包（Version + Lua + AB）", GUILayout.Height(36)))
            BuildAll();

        if (GUILayout.Button("仅生成 Version 文件"))
            GenerateVersionOnly();
    }

    // ══════════════════════════════════════════════════════════════════════
    //  构建入口
    // ══════════════════════════════════════════════════════════════════════

    private void BuildAll()
    {
        SaveToDisk();
        GenerateVersionFile();
        EnsureLuaFolder();

        int count = 0;
        if (buildAndroid) { BuildSingleAB("AssetBundles_Android", BuildTarget.Android); count++; }
        if (buildWindows) { BuildSingleAB("AssetBundles_Windows", BuildTarget.StandaloneWindows64); count++; }
        if (buildIOS)     { BuildSingleAB("AssetBundles_IOS", BuildTarget.iOS); count++; }

        AssetDatabase.Refresh();
        Debug.Log($"🎉 打包完成：{count} 个平台 + version.json");
    }

    private void GenerateVersionOnly()
    {
        SaveToDisk();
        GenerateVersionFile();
        EnsureLuaFolder();
        AssetDatabase.Refresh();
        Debug.Log("✅ version.json 已生成");
    }

    // ══════════════════════════════════════════════════════════════════════
    //  快捷菜单
    // ══════════════════════════════════════════════════════════════════════

    [MenuItem("Assets/AB包/仅构建 Android", false, 1)]
    private static void BuildAndroidOnly()
    {
        GenerateVersionFile();
        EnsureLuaFolder();
        BuildSingleAB("AssetBundles_Android", BuildTarget.Android);
        AssetDatabase.Refresh();
        Debug.Log("🎉 Android 打包完成");
    }

    [MenuItem("Assets/AB包/仅构建 Windows", false, 2)]
    private static void BuildWindowsOnly()
    {
        GenerateVersionFile();
        EnsureLuaFolder();
        BuildSingleAB("AssetBundles_Windows", BuildTarget.StandaloneWindows64);
        AssetDatabase.Refresh();
        Debug.Log("🎉 Windows 打包完成");
    }

    [MenuItem("Assets/AB包/仅构建 iOS", false, 3)]
    private static void BuildIOSOnly()
    {
        GenerateVersionFile();
        EnsureLuaFolder();
        BuildSingleAB("AssetBundles_IOS", BuildTarget.iOS);
        AssetDatabase.Refresh();
        Debug.Log("🎉 iOS 打包完成");
    }

    [MenuItem("Assets/AB包/清理资源", false, 10)]
    private static void CleanBeforeBuild()
    {
        AssetDatabase.Refresh();
        EditorUtility.UnloadUnusedAssetsImmediate();
        System.GC.Collect();
        Debug.Log("资源清理完成");
    }

    // ══════════════════════════════════════════════════════════════════════
    //  核心生成方法
    // ══════════════════════════════════════════════════════════════════════

    private static void BuildSingleAB(string folderName, BuildTarget target)
    {
        string outputPath = Path.Combine(Application.streamingAssetsPath, folderName);
        if (Directory.Exists(outputPath))
            Directory.Delete(outputPath, true);
        Directory.CreateDirectory(outputPath);

        BuildPipeline.BuildAssetBundles(
            outputPath,
            BuildAssetBundleOptions.ChunkBasedCompression,
            target);

        Debug.Log($"✓ {target} → {outputPath}");
    }

    private static void GenerateVersionFile()
    {
        var cfg = ReadConfigFromDisk();
        string dir = Path.Combine(Application.streamingAssetsPath, "version");
        Directory.CreateDirectory(dir);

        var data = new VersionData
        {
            version       = cfg.version,
            updateContent = cfg.updateContent,
        };
        File.WriteAllText(Path.Combine(dir, "version.json"), JsonUtility.ToJson(data, true));
        Debug.Log($"✓ version.json（{cfg.version}）");
    }

    private static void EnsureLuaFolder()
    {
        string dir = Path.Combine(Application.streamingAssetsPath, "lua");
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
            Debug.Log($"✓ lua/ 目录已创建（{dir}）");
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    //  配置持久化
    // ══════════════════════════════════════════════════════════════════════

    private void LoadFromDisk()
    {
        var cfg = ReadConfigFromDisk();
        versionNumber = cfg.version;
        updateContent = cfg.updateContent;
    }

    private void SaveToDisk()
    {
        WriteConfigToDisk(new ABBuildConfig
        {
            version       = versionNumber,
            updateContent = updateContent,
        });
    }

    private static ABBuildConfig ReadConfigFromDisk()
    {
        string path = ConfigFilePath;
        if (File.Exists(path))
        {
            try
            {
                return JsonUtility.FromJson<ABBuildConfig>(File.ReadAllText(path))
                       ?? new ABBuildConfig();
            }
            catch { }
        }
        return new ABBuildConfig();
    }

    private static void WriteConfigToDisk(ABBuildConfig cfg)
    {
        if (!Directory.Exists(ConfigDir))
            Directory.CreateDirectory(ConfigDir);
        File.WriteAllText(ConfigFilePath, JsonUtility.ToJson(cfg, true));
    }
}
