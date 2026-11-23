using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public readonly string uiRootPath = "UI/"; // UI 根目录
    private Dictionary<string, UIBase> uis; // UI 链表
    private Dictionary<string, UIBase> uiCloses; // 关闭的UI链表
    private long uiIdCounter = 0; // UI ID 计数器
    [SerializeField] private Transform uiRoot; // UI 根节点
    [SerializeField] private float cleanupTime = 300f; // 清理时间
    [SerializeField] private float timer = 0f; // 计时器

    private Dictionary<string, UIBase> Uis {
        get
        {
            if(uis == null)
            {
                uis = new Dictionary<string, UIBase>();
            }
            return uis;
        } set => uis = value;
    }
    private Dictionary<string, UIBase> UICloses { get
        {
            if (uiCloses == null)
            {
                uiCloses = new Dictionary<string, UIBase>();
            }
            return uiCloses;
        }
        set => uiCloses = value; 
    }

    //UI清理事件
    public Action OnUIClear{get; set;}

    void Awake()
    {
        if (uiRoot == null)
        {
            uiRoot = transform;
        }


        SceneManager.sceneLoaded += ScanScene; // 注册场景加载事件
    }

    void Update()
    {
        // 检查关闭的UI
        if (UICloses.Count > 0)
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

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= ScanScene; // 注销场景加载事件
    }

    /// <summary>
    /// 切换场景时自动获取场景中的UIRoot
    /// </summary>
    /// <param name="scene"></param>
    /// <param name="mode"></param>
    private void ScanScene(Scene scene, LoadSceneMode mode)
    {
        //处理UI
        DestroyAllCloseUI();
        //处理值为空的UI
        var uisName=new List<string>();
        foreach (var ui in Uis)
        {
            if(ui.Value == null)
                uisName.Add(ui.Key);
        }
        foreach (var ui in uisName)
        {
            Uis.Remove(ui);
        }



        //处理根对象
        if(uiRoot != null) return;

        //获取场景中的所有游戏对象(包括未激活的)
        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();

        //筛选出有标记的游戏对象
        foreach (GameObject obj in allObjects)
        {
            obj.TryGetComponent<UIRoot>(out var ui);

            if(ui != null)
                uiRoot=obj.transform;
        }
    }

    /// <summary>
    /// 注册UI
    /// </summary>
    /// <param name="ui"></param>
    /// <param name="uiid"></param>
    /// <returns></returns>
    public string Register(UIBase ui,string uiid)
    {
        uiIdCounter++;
        string uiId = uiid + "_" + uiIdCounter;
        Uis.Add(uiId, ui);

        return uiId;
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
        foreach (var ui in UICloses.Values)
        {
            if(ui != null)
                Destroy(ui.gameObject);
        }
        UICloses.Clear();

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

        foreach (var ui in Uis.Values)
        {
            Destroy(ui.gameObject);
        }
        Uis.Clear();

    }

    /// <summary>
    /// 打开UI界面
    /// </summary>
    /// <param name="uiName"></param>
    /// <returns></returns>
    public UIBase OpenUI(string uiName,Vector3 pos=default,Quaternion quaternion=default,Transform parent = null)
    {
        //先在关闭的UI中查找
        if(UICloses.Count > 0)
        {
            string key="";
            UIBase value = null;
            foreach (var ui in UICloses)
            {
                if (SplitUIID(ui.Key) == uiName)
                {
                    ui.Value.gameObject.SetActive(true);
                    Uis.Add(ui.Key, ui.Value);
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
                if (parent != null)
                {
                    value.transform.SetParent(parent);
                }

                UICloses.Remove(key);
                return value;
            }


        }

        //如果找不到或没有
        return CreateUI(uiName,pos, quaternion,parent);
    }

    /// <summary>
    /// 取一个真正能显示 UI 的根节点。
    /// 序列化的 uiRoot 可能是被销毁/未配置/不在激活层级的对象：那样 UI 会被实例化到
    /// 没有 Canvas 的场景根上，界面「创建成功但看不见」。这里每次创建前重新校验，
    /// 找不到就回退到场景中的 UIRoot，再找不到就自动建一个全屏 Canvas。
    /// </summary>
    /// <summary>根节点必须存在、在激活层级里，并且属于一个启用的 Canvas，否则 UI 会看不见。</summary>
    private static bool IsUsableRoot(Transform root)
    {
        if (root == null) return false;
        if (!root.gameObject.activeInHierarchy) return false;

        var canvas = root.GetComponentInParent<Canvas>();
        return canvas != null && canvas.enabled;
    }

    private Transform ResolveUIRoot()
    {
        if (IsUsableRoot(uiRoot)) return uiRoot;

        //切场景后旧引用可能已销毁；也可能是被禁用/未绑定的 Canvas，重新在场景里找
        var roots = Resources.FindObjectsOfTypeAll<UIRoot>();
        foreach (var root in roots)
        {
            if (root == null) continue;
            if (!root.gameObject.scene.IsValid()) continue;       // 排除 Prefab 资产
            if (!IsUsableRoot(root.transform)) continue;

            uiRoot = root.transform;
            Debug.LogWarning($"[UIManager] uiRoot 不可用，已重新绑定场景中的 UIRoot：{root.name}");
            return uiRoot;
        }

        //场景里连 UIRoot 都没有：自动建一个，保证界面至少能显示出来
        var go = new GameObject("UIRoot(自动)", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(UIRoot));

        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        DontDestroyOnLoad(go);
        uiRoot = go.transform;
        Debug.LogWarning("[UIManager] 场景里没有可用的 UIRoot，已自动创建 UIRoot(自动) 承载界面");
        return uiRoot;
    }

    /// <summary>
    /// 创建UI界面
    /// </summary>
    /// <param name="uiName"></param>
    /// <returns></returns>
    private UIBase CreateUI(string uiName,Vector3 pos=default,Quaternion quaternion=default,Transform parent = null)
    {
        //加载UI预制体
        GameObject prefab = Resources.Load<GameObject>(uiRootPath + uiName);
        if (prefab == null)
        {
            Debug.LogError($"未找到UI预制体，路径：{uiRootPath + uiName}");
            return null;
        }

        //实例化UI
        GameObject uiInstance = Instantiate(prefab, ResolveUIRoot());

        if (!uiInstance.activeInHierarchy)
        {
            Debug.LogError($"[UIManager] UI {uiName} 实例化后不在激活层级，父节点：{uiInstance.transform.parent?.name}，界面不会显示");
        }
        if (pos != default)
        {
            uiInstance.transform.position = pos;
        }
        if (quaternion != default)
        {
            uiInstance.transform.rotation = quaternion;
        }
        if (parent != null)
        {
            uiInstance.transform.SetParent(parent);
        }
        uiInstance.TryGetComponent<UIBase>(out UIBase component);

        if (!component)
        {
            Debug.LogError("该UI没有UIBase组件！,无法创建UI，路径："+uiRootPath + uiName);
            return null;
        }

        //检查是否独立化
        if (!component.independence)
        {
            // 入栈
            uiIdCounter++;
            var newId = uiName + "_" + uiIdCounter;
            component.id = newId;
            Uis.Add(newId, component);
        }

        return component;
    }

    /// <summary>
    /// 关闭UI界面
    /// </summary>
    /// <param name="ui"></param>
    public void CloseUI(GameObject ui)
    {
        var UIBase = ui.TryGetComponent<UIBase>(out UIBase component);

        if (!UIBase)
        {
            Debug.LogError("该UI没有UIBase组件！");
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
        if (Uis.ContainsKey(id))
        {
            UIBase ui = Uis[id];
            Uis.Remove(id);

            //销毁子UI
            ui.Disable();

            //重置UI
            ui.ResetUI();

            // 隐藏UI
            ui.gameObject.SetActive(false);

            // 入栈到关闭列表
            UICloses.Add(id, ui);
        }
        else
        {
            Debug.LogWarning("关闭UI失败，未找到UI ID：" + id);
        }
    }
}
