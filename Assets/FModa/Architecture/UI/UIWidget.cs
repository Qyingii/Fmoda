using UnityEngine;

/// <summary>
/// UI 组件基类 —— 统一管理 DataCenter 事件的订阅/取消订阅。
///
/// 为什么需要这个类？
///   UI 不需要模块系统（模块有优先级、接口注册、逻辑帧 Tick），
///   UI 只需要"在显示时订阅数据 → 数据变了刷新 → 销毁时取消订阅"。
///   UIWidget 就是做这个的最小抽象。
///
/// 使用方式：
/// <code>
/// public class HpBar : UIWidget
/// {
///     [SerializeField] private TextMeshProUGUI _text;
///
///     protected override void OnBind()
///     {
///         DataManager.Combat.OnHpChanged += OnHpChanged;
///     }
///     protected override void OnUnbind()
///     {
///         DataManager.Combat.OnHpChanged -= OnHpChanged;
///     }
///     private void OnHpChanged(int id, int old, int newHp)
///     {
///         _text.text = newHp.ToString();
///     }
/// }
/// </code>
/// </summary>
public abstract class UIWidget : MonoBehaviour
{
    private bool _isBound;

    private void Start()
    {
        OnBind();
        _isBound = true;
        OnRefresh(); // 绑定后立即同步一次当前状态
    }

    private void OnDestroy()
    {
        if (_isBound) OnUnbind();
    }

    /// <summary> 在此订阅 DataCenter 的事件 </summary>
    protected abstract void OnBind();

    /// <summary> 在此取消所有订阅 </summary>
    protected abstract void OnUnbind();

    /// <summary>
    /// 初始状态同步（可选重写）。
    /// 在 OnBind 完成后自动调用，用于把数据中心的当前值刷新到 UI 上。
    /// </summary>
    protected virtual void OnRefresh() { }
}
