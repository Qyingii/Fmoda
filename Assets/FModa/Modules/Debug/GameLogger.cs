using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

public class GameLogger : MonoBehaviour
{
    public GameObject logPanel;        // 日志面板（包含Scroll View）
    public GameObject logItemPrefab; // 日志项的预制体（带TextMeshProUGUI）
    public Transform contentParent;   // Scroll View的Content对象
    public ScrollRect scrollRect;    // Scroll View组件
    public int maxLogItems = 100;    // 最大日志数量（避免内存泄漏）
    public string lastLogStr = "";// 最后一条日志
    public TextMeshProUGUI lastLogText;// 最后一条日志的文本
    private int logCount = 1;// 日志计数器

    [SerializeField] private float timeLimit = 10f;// 时间限制
    private float time;// 计时器

    private Queue<GameObject> logItems = new Queue<GameObject>();// 存储日志项

    void Awake()
    {
        DontDestroyOnLoad(logPanel); // 确保日志管理器在场景切换时不会被销毁
        Application.logMessageReceived += HandleLog;// 注册日志回调
        logPanel.SetActive(false);
    }

    // private void OnEnable()
    // {
    //     Application.logMessageReceived += HandleLog;
    // }

    // private void OnDisable()
    // {
    //     Application.logMessageReceived -= HandleLog;
    // }

    void Update()
    {
        time += Time.deltaTime;
        if (time >= timeLimit)
        {
            time = 0;
            logPanel.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        Application.logMessageReceived -= HandleLog; // 确保在销毁时注销回调
    }

    private void HandleLog(string logString, string stackTrace, LogType type)
    {
        // 过滤掉Unity的默认日志（可选）
        if (logString.StartsWith("Log Type:") || logString.StartsWith("Log Assertion.") || logString.StartsWith("Log Exception.") || logString.StartsWith("Log Warning.")
        ) return;

        //开启日志面板
        logPanel.SetActive(true);

        // 创建新日志项
        if (logString == lastLogStr)
        {
            logCount++;
            lastLogText.text = logString + " *" + logCount;
        }
        else
        {
            GameObject newLogItem = Instantiate(logItemPrefab, contentParent);
            lastLogText = newLogItem.GetComponent<TextMeshProUGUI>();
            logCount = 1;

            // 设置日志内容和颜色
            lastLogText.text = logString;
            lastLogText.color = GetLogColor(type);
            lastLogStr = logString;// 更新最后一条日志

            // 限制日志数量
            logItems.Enqueue(newLogItem);
            if (logItems.Count > maxLogItems)
            {
                Destroy(logItems.Dequeue());
            }

            // 强制Canvas更新布局（立即生效）
            Canvas.ForceUpdateCanvases();
        }

        // 滚动到底部
        //scrollRect.verticalNormalizedPosition = 0;

    }

    private Color GetLogColor(LogType type)
    {
        return type switch
        {
            LogType.Error => Color.red,
            LogType.Warning => Color.yellow,
            _ => Color.white,
        };
    }
}