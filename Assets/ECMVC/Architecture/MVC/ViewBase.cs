using UnityEngine;

// MVC 架构中 View 层的基类。
//
// View 的职责：
//   1. 持有所有 UI 组件的引用（Inspector 序列化字段）
//   2. 通过 BindEvents() 订阅 Model 的事件，数据变化时自动刷新显示
//   3. 提供公共的显示方法（如 ShowPanel / UpdateText）供自身订阅调用
//
// View 的禁忌：
//   - 不处理任何 Input（键盘/鼠标）
//   - 不持有 Controller 的引用，也不调用 Controller 方法
//   - 不直接读写 Model 数据（只通过 Model 事件被动接收）
//
// 用法示例：
//   public class GameProgressView : ViewBase
//   {
//       [SerializeField] private Slider slider;
//
//       private GameProgressModel _model;
//
//       public void Bind(GameProgressModel model)
//       {
//           _model = model;
//           BindEvents();
//       }
//
//       protected override void BindEvents()   => _model.OnProgressChanged += OnProgressChanged;
//       protected override void UnbindEvents() => _model.OnProgressChanged -= OnProgressChanged;
//
//       private void OnProgressChanged(float v) => slider.value = v;
//   }
public abstract class ViewBase : MonoBehaviour
{
    // 子类在此订阅 Model 的所有事件
    protected abstract void BindEvents();

    // 子类在此取消所有事件订阅，防止对象销毁后产生空引用回调
    protected abstract void UnbindEvents();

    protected virtual void OnDestroy()
    {
        if (_isBound) UnbindEvents();
    }

    private bool _isBound;

    protected void SetBound() => _isBound = true;
}
