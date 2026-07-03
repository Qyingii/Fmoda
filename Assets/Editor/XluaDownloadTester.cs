using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using ECMVC.HotUpdate;

/// <summary>
/// XLua 打包下载功能测试窗口
/// 菜单: ECMVC → XLua 下载测试工具
/// </summary>
public class XluaDownloadTester : EditorWindow
{
    // ===== 配置 =====
    private string serverUrl = "https://localhost:6567";
    private string childPath = "lua";
    private Vector2 logScrollPos;
    private List<LogEntry> logs = new List<LogEntry>();
    private bool isRunning;

    // ===== 菜单入口 =====

    [MenuItem("ECMVC/XLua 下载测试工具")]
    private static void ShowWindow()
    {
        var win = GetWindow<XluaDownloadTester>("XLua下载测试");
        win.minSize = new Vector2(540, 460);
        win.Show();
    }

    // ===== 生命周期 =====

    private void OnEnable()
    {
        serverUrl = EditorPrefs.GetString("XluaTester_Url", "https://localhost:6567");
        childPath = EditorPrefs.GetString("XluaTester_ChildPath", "lua");
        Application.logMessageReceived += OnLogMessage;
    }

    private void OnDisable()
    {
        Application.logMessageReceived -= OnLogMessage;
        EditorPrefs.SetString("XluaTester_Url", serverUrl);
        EditorPrefs.SetString("XluaTester_ChildPath", childPath);
    }

    // ===== 日志捕获 =====

    private class LogEntry
    {
        public string text;
        public LogType type;
    }

    private void OnLogMessage(string condition, string stacktrace, LogType type)
    {
        // 只显示一定量的日志，防止内存膨胀
        logs.Add(new LogEntry { text = condition, type = type });
        if (logs.Count > 500)
            logs.RemoveRange(0, logs.Count - 500);

        logScrollPos.y = float.MaxValue;
        Repaint();
    }

    // ===== GUI =====

    private void OnGUI()
    {
        DrawHeader();
        DrawUrlConfig();
        DrawStepTests();
        DrawQuickActions();
        DrawStatusBar();
        DrawLogArea();
    }

    private void DrawHeader()
    {
        EditorGUILayout.Space(4);
        var titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
        EditorGUILayout.LabelField("🧪 XLua 下载流程测试", titleStyle);
        EditorGUILayout.LabelField("按步骤依次测试，观察右侧日志输出", EditorStyles.miniLabel);
        EditorGUILayout.Space(4);
    }

    private void DrawUrlConfig()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("服务器配置", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        serverUrl = EditorGUILayout.TextField("URL", serverUrl);
        if (GUILayout.Button("保存", GUILayout.Width(50)))
            EditorPrefs.SetString("XluaTester_Url", serverUrl);
        EditorGUILayout.EndHorizontal();

        childPath = EditorGUILayout.TextField("子路径（childPath）", childPath);

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(2);
    }

    private void DrawStepTests()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("步骤测试", EditorStyles.boldLabel);

        GUI.enabled = !isRunning;

        // 第一行
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("1️⃣  获取文件列表", GUILayout.Height(30)))
            RunAction("获取文件列表", () => WithManager(m => m.StartGetFileList(childPath)));
        if (GUILayout.Button("2️⃣  扫描本地文件", GUILayout.Height(30)))
            RunAction("扫描本地文件", () => WithManager(m => m.StartScanLocal(childPath)));
        EditorGUILayout.EndHorizontal();

        // 第二行
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("3️⃣  对比并下载", GUILayout.Height(30)))
            RunAction("对比下载", () => WithManager(m => m.StartCompareDownload(childPath)));
        if (GUILayout.Button("4️⃣  完整更新流程", GUILayout.Height(30)))
            RunAction("完整更新", () => WithManager(m =>
            {
                m.ServerUrl = serverUrl;
                m.StartFullUpdate();
            }));
        EditorGUILayout.EndHorizontal();

        // 第三行：单独的按钮
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("🔍  仅检查更新", GUILayout.Height(30)))
            RunAction("检查更新", () => WithManager(m =>
            {
                m.ServerUrl = serverUrl;
                m.StartCheckUpdate();
            }));
        if (GUILayout.Button("📋  打印文件列表到控制台", GUILayout.Height(30)))
            PrintFileList();
        EditorGUILayout.EndHorizontal();

        GUI.enabled = true;
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(2);
    }

    private void DrawQuickActions()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("快捷操作", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("➕ 创建 XLuaManager (场景)"))
            CreateManagerInScene();
        if (GUILayout.Button("📂 打开 PersistentData"))
            OpenInExplorer(Application.persistentDataPath);
        if (GUILayout.Button("📂 打开 StreamingAssets"))
            OpenInExplorer(Application.streamingAssetsPath);
        EditorGUILayout.EndHorizontal();

        // 测试用：先打 AB 包再测试
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("📦 打开 AB 打包窗口"))
            EditorApplication.ExecuteMenuItem("Assets/AB包/打开打包窗口");
        if (GUILayout.Button("🧹 清理日志"))
        {
            logs.Clear();
            Repaint();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(2);
    }

    private void DrawStatusBar()
    {
        var mgr = HotUpdateManager.Instance;
        bool hasManager = mgr != null;

        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

        // 状态灯
        GUI.color = hasManager ? Color.green : Color.gray;
        EditorGUILayout.LabelField(hasManager ? "●" : "○", GUILayout.Width(16));
        GUI.color = Color.white;

        if (hasManager)
        {
            EditorGUILayout.LabelField(
                $"XLuaManager 已就绪  |  服务器: {mgr.ServerUrl}  |  远程 {mgr.ServerFileCount} / 本地 {mgr.LocalFileCount} 个文件",
                EditorStyles.miniLabel);
        }
        else
        {
            EditorGUILayout.LabelField("XLuaManager 未在场景中找到 — 点击上方「创建 XLuaManager」", EditorStyles.miniLabel);
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(2);
    }

    private void DrawLogArea()
    {
        EditorGUILayout.LabelField("日志输出", EditorStyles.boldLabel);

        logScrollPos = EditorGUILayout.BeginScrollView(logScrollPos,
            GUILayout.ExpandHeight(true));

        // 逐行绘制带颜色的日志
        foreach (var entry in logs)
        {
            GUI.color = entry.type switch
            {
                LogType.Error   => Color.red,
                LogType.Warning => Color.yellow,
                _               => Color.white,
            };
            EditorGUILayout.SelectableLabel(entry.text, EditorStyles.miniLabel,
                GUILayout.ExpandWidth(true));
        }
        GUI.color = Color.white;

        EditorGUILayout.EndScrollView();

        // 底部按钮
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("清空日志", GUILayout.Width(80)))
        {
            logs.Clear();
            Repaint();
        }
        if (GUILayout.Button("复制全部", GUILayout.Width(80)))
        {
            string all = string.Join("\n", logs.ConvertAll(e => e.text));
            EditorGUIUtility.systemCopyBuffer = all;
        }
        EditorGUILayout.LabelField($"共 {logs.Count} 条", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();
    }

    // ===== 辅助方法 =====

    private void RunAction(string label, System.Action action)
    {
        Debug.Log($"=== {label} ===");
        isRunning = true;
        try
        {
            action();
        }
        finally
        {
            isRunning = false;
        }
    }

    private void WithManager(System.Action<HotUpdateManager> action)
    {
        var mgr = HotUpdateManager.Instance;
        if (mgr == null)
        {
            Debug.LogError("XLuaManager 未在场景中！请先点击「创建 XLuaManager」");
            return;
        }
        mgr.ServerUrl = serverUrl;
        action(mgr);
    }

    private void PrintFileList()
    {
        var mgr = HotUpdateManager.Instance;
        if (mgr == null)
        {
            Debug.LogError("XLuaManager 未在场景中");
            return;
        }
        Debug.Log($"服务器文件数: {mgr.ServerFileCount}, 本地文件数: {mgr.LocalFileCount}");
    }

    private void CreateManagerInScene()
    {
        if (HotUpdateManager.Instance != null)
        {
            EditorUtility.DisplayDialog("提示", "场景中已有 XLuaManager", "确定");
            return;
        }

        var go = new GameObject("XLuaManager");
        go.AddComponent<HotUpdateManager>();
        Undo.RegisterCreatedObjectUndo(go, "Create XLuaManager");

        Debug.Log("✅ 已创建 XLuaManager，可在 Hierarchy 中查看");
        EditorGUIUtility.PingObject(go);
    }

    private void OpenInExplorer(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
        EditorUtility.RevealInFinder(path);
    }
}
