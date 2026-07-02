using UnityEngine;

// MVC 架构中 Controller 层的基类。
//
// Controller 的职责：
//   1. 监听用户输入（Input.GetMouseButton / UI Button.onClick 等）
//   2. 将输入翻译为对 Model 的操作（调用 Model 的方法或修改其属性）
//   3. 可以持有 Model 的引用
//
// Controller 的禁忌：
//   - 不直接操作任何 UI 元素（Text.text / Image.color 等）
//   - 不持有 View 的引用（UI 刷新由 Model 事件驱动 View 自行完成）
//
// 用法示例：
//   public class MapMakeController : ControllerBase
//   {
//       private MapMakeModel _model;
//
//       public void Init(MapMakeModel model) => _model = model;
//
//       private void Update()
//       {
//           if (Input.GetMouseButton(0)) _model.Draw(GetWorldPos());
//       }
//
//       public void OnUndoButton() => _model.Undo();  // UI Button 直接调用此方法
//   }
public abstract class ControllerBase : MonoBehaviour { }
