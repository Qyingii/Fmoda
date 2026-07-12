// Controller：处理用户输入，调用 Model 方法
// 不引用 View，不操作任何 UI 元素（Text/Image 等）
public class CounterController : ControllerBase
{
    private CounterModel _model;

    // 由 Bootstrap 在 Awake 中调用
    public void Init(CounterModel model) => _model = model;

    // 将以下方法绑定到 UI Button 的 OnClick 事件
    public void OnIncrementButton() => _model.Increment();
    public void OnDecrementButton() => _model.Decrement();
    public void OnResetButton()     => _model.Reset();
}
