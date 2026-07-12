using TMPro;
using UnityEngine;

// View：订阅 Model 事件，数据变化时自动刷新 UI
// 不处理任何 Input，不引用 Controller
public class CounterView : ViewBase
{
    [SerializeField] private TextMeshProUGUI _countText;

    private CounterModel _model;

    // 由 Bootstrap 在 Awake 中调用，完成 Model 绑定
    public void Bind(CounterModel model)
    {
        _model = model;
        SetBound();
        BindEvents();
        Refresh(_model.Count); // 立即同步当前值，避免首帧显示空白
    }

    protected override void BindEvents()   => _model.OnCountChanged += Refresh;
    protected override void UnbindEvents() => _model.OnCountChanged -= Refresh;

    private void Refresh(int count) => _countText.text = count.ToString();
}
