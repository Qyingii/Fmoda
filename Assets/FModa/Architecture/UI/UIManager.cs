using System;
using System.Collections.Generic;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    public readonly string uiRootPath = "UI/"; // UI 根目录
    private Dictionary<string, UIWidget> uis; // UI 链表
    private Dictionary<string, UIWidget> uiCloses; // 关闭的UI链表
    private int uiIdCounter = 0; // UI ID 计数器
    [SerializeField] private Transform uiRoot; // UI 根节点
    [SerializeField] private float cleanupTime = 300f; // 清理时间
    [SerializeField] private float timer = 0f; // 计时器

    //UI清理事件
    public Action OnUIClear{get; set;}

    void Awake()
    {
        if (uiRoot == null)
        {
            uiRoot = transform;
        }

        uis = new Dictionary<string, UIWidget>();
        uiCloses = new Dictionary<string, UIWidget>();
    }

    void Update()
    {
        // 检查关闭的UI
        if (uiCloses.Count > 0)
        {
            timer += Time.deltaTime; // 计时器增加
            if (timer >= cleanupTime)
            {
                // 清理关闭的UI
                DestroyAllCloseUI();  
                
                timer = 0f; // 重置计时器

                Debug.Log("清理关闭的UI中...");
            }
        }
    }

    //拆分UIID
    public string SplitUIID(string id)
    {
        string[] split = id.Split('_');
        return split[0];
    }

    /// <summary>
    /// 销毁全部已关闭的UI
    /// </summary>
    public void DestroyAllCloseUI()
    {
        foreach (var ui in uiCloses.Values)
        {
            Destroy(ui.gameObject);
        }
        uiCloses.Clear();

        //通知有子UI的UI进行清理
        OnUIClear?.Invoke();
    }

    /// <summary>
    /// 销毁全部UI
    /// </summary>
    public void DestroyAllUI()
    {
        //先销毁全部关闭的UI
        DestroyAllCloseUI();

        foreach (var ui in uis.Values)
        {
            Destroy(ui.gameObject);
        }
        uis.Clear();

    }

    /// <summary>
    /// 打开UI界面
    /// </summary>
    /// <param name="uiName"></param>
    /// <returns></returns>
    public UIWidget OpenUI(string uiName,Vector3 pos=default,Quaternion quaternion=default)
    {
        //先在关闭的UI中查找
        if(uiCloses.Count > 0)
        {
            string key="";
            UIWidget value = null;
            foreach (var ui in uiCloses)
            {
                if (SplitUIID(ui.Key) == uiName)
                {
                    ui.Value.gameObject.SetActive(true);
                    uis.Add(ui.Key, ui.Value);
                    key = ui.Key;
                    value = ui.Value;
                    break;
                }
            }
            
            if(!string.IsNullOrEmpty(key) && value != null)
            {
                if(pos != default)
                {
                    value.transform.position = pos;
                }

                if(quaternion != default)
                {
                    value.transform.rotation = quaternion;
                }

                uiCloses.Remove(key);
                return value;
            }


        }

        //如果找不到或没有
        return CreateUI(uiName,pos, quaternion);
    }

    /// <summary>
    /// 创建UI界面
    /// </summary>
    /// <param name="uiName"></param>
    /// <returns></returns>
    private UIWidget CreateUI(string uiName,Vector3 pos=default,Quaternion quaternion=default)
    {
        //加载UI预制体
        GameObject prefab = Resources.Load<GameObject>(uiRootPath + uiName);
        if (prefab == null)
        {
            Debug.LogError($"未找到UI预制体，路径：{uiRootPath + uiName}");
            return null;
        }

        //实例化UI
        GameObject uiInstance = Instantiate(prefab,uiRoot);
        if (pos != default)
        {
            uiInstance.transform.position = pos;
        }

        if (quaternion != default)
        {
            uiInstance.transform.rotation = quaternion;
        }
        uiInstance.TryGetComponent<UIWidget>(out UIWidget component);

        if (!component)
        {
            Debug.LogError("该UI没有UIWidget组件！,无法创建UI，路径："+uiRootPath + uiName);
            return null;
        }

        // 入栈
        uiIdCounter++;
        var newId = uiName + "_" + uiIdCounter;
        component.id = newId;
        uis.Add(newId, component);
        return component;
    }

    /// <summary>
    /// 关闭UI界面
    /// </summary>
    /// <param name="ui"></param>
    public void CloseUI(GameObject ui)
    {
        var uiwidget = ui.TryGetComponent<UIWidget>(out UIWidget component);

        if (!uiwidget)
        {
            Debug.LogError("该UI没有UIWidget组件！");
        }

        // 获取UI ID
        var uiid = component.id;

        //关闭UI界面
        CloseUI(uiid);
    }

    /// <summary>
    /// 关闭UI界面
    /// </summary>
    /// <param name="id"></param>
    public void CloseUI(string id)
    {

        // 出栈
        if (uis.ContainsKey(id))
        {
            UIWidget ui = uis[id];
            uis.Remove(id);

            //销毁子UI
            ui.Disable();

            //重置UI
            ui.ResetUI();

            // 隐藏UI
            ui.gameObject.SetActive(false);

            // 入栈到关闭列表
            uiCloses.Add(id, ui);
        }
        else
        {
            Debug.LogWarning("关闭UI失败，未找到UI ID：" + id);
        }
    }
}
