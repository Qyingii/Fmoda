using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// UI 组件基类 —— 统一管理事件订阅的绑定/解绑时机。
///
/// 为什么需要这个类？
///   UI 不需要模块系统（模块有优先级、接口注册、逻辑帧 Tick），
///   UI 只需要"在显示时订阅 → 数据变了刷新 → 销毁时取消订阅"。
///   UIBase 就是做这个的最小抽象。
///
/// 注意：订阅的一律是**模块**暴露的事件，不要直接订阅 DataCenter。
///   README 的约定是「渲染器、UI 都必须通过模块来获取数据」，
///   直接订阅数据中心会绕过模块，视图与数据的耦合就没有中间层兜底了。
///
/// 使用方式：
/// <code>
/// public class HpBar : UIBase
/// {
///     [SerializeField] private TextMeshProUGUI _text;
///
///     private ICombatManager _combat;
///
///     protected override void OnBind()
///     {
///         _combat = GF.GetModule&lt;CombatManager&gt;();
///         _combat.OnHpChanged += OnHpChanged;
///     }
///     protected override void OnUnbind()
///     {
///         _combat.OnHpChanged -= OnHpChanged;
///     }
///     private void OnHpChanged(int id, int old, int newHp)
///     {
///         _text.text = newHp.ToString();
///     }
/// }
/// </code>
/// </summary>
public abstract class UIBase : MonoBehaviour
{
    /// <summary>
    /// 绑定状态
    /// </summary>
    private bool _isBound;

    /// <summary>
    /// UI的唯一标识,管理器自动分配，请勿手动修改
    /// </summary>
    public string id;

    /// <summary>
    /// 是否独立化,独立化的UI不通过UIManager管理
    /// </summary>
    [Tooltip("是否独立化,独立化的UI不通过UIManager管理")]
    public bool independence=false;

    /// <summary>
    /// 子UI字典
    /// </summary>
    public Dictionary<string, UIBase> subUIs;

    void Start()
    {
        if (!independence && !id.Contains("_"))
        {
            id = gameObject.name;
            id = GF.uiManager.Register(this, id);
        }
    }

    void OnEnable()
    {
        if (_isBound) return;

        //回到
        OnBind();
        _isBound = true;
        OnRefresh(); // 绑定后立即同步一次当前状态
    }

    void OnDisable()
    {
        if (_isBound) OnUnbind();
        _isBound = false;
    }

    private void OnDestroy()
    {
        if (_isBound) OnUnbind();
    }

    /// <summary> 在此订阅模块暴露的事件 </summary>
    protected abstract void OnBind();

    /// <summary> 在此取消所有订阅 </summary>
    protected abstract void OnUnbind();

    /// <summary>
    /// 初始状态同步（可选重写）。
    /// 在 OnBind 完成后自动调用，用于把模块里的当前值刷新到 UI 上。
    /// </summary>
    protected virtual void OnRefresh() { }

    /// <summary>
    /// 关闭UI
    /// </summary>
    public virtual void Close()
    {
        GF.uiManager.CloseUI(id);
    }

    /// <summary>
    /// 重置UI
    /// </summary>
    public virtual void ResetUI(){}

    /// <summary>
    /// 打开子UI
    /// </summary>
    /// <param name="subUIName"></param>
    public void OpenSubUI(string subUIName,Vector3 pos=default,Quaternion quaternion=default)
    {
        if(subUIs==null)
        {
            subUIs = new Dictionary<string, UIBase>();
            
            //订阅UI清理事件
            GF.uiManager.OnUIClear += ClearSubUI;
        }

        var subUI=GF.uiManager.OpenUI(subUIName,pos,quaternion);

        //设置父级关系
        subUI.transform.SetParent(transform);

        if(!subUIs.ContainsKey(subUI.id))
        {
            subUIs.Add(subUI.id, subUI);
        }
    }

    /// <summary>
    /// 销毁,销毁时会关闭全部子UI
    /// </summary>
    public void Disable()
    {
        if(subUIs!=null)
        {
            //取消订阅事件
            GF.uiManager.OnUIClear -= ClearSubUI;

            if (subUIs.Count > 0)
            {
                //销毁子UI
                foreach (var subUI in subUIs)
                {
                    if (subUI.Value != null)
                    {
                        GF.uiManager.CloseUI(subUI.Key);
                    }
                }
            }

            subUIs = null; //置空
        }

        //重置位置
        //加载UI预制体
        var path= GF.uiManager.uiRootPath + GF.uiManager.SplitUIID(id);
        GameObject prefab = Resources.Load<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogWarning($"未找到UI预制体，路径：{path},无法重置位置");
            return;
        }

        transform.localPosition = prefab.transform.localPosition;
        transform.localScale = prefab.transform.localScale;
        transform.localRotation = prefab.transform.localRotation;
    }

    //清理子UI
    private void ClearSubUI()
    {
        if (subUIs != null && subUIs.Count > 0)
        {
            List<string> keys=new();

            foreach (var subUI in subUIs)
            {
                if(subUI.Value==null)
                {
                    keys.Add(subUI.Key);
                }
            }

            foreach (var key in keys)
            {
                subUIs.Remove(key);
            }
        }
    }
}
