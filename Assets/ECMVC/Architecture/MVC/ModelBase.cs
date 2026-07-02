// MVC 架构中 Model 层的基类。
//
// Model 的职责：
//   1. 拥有并管理数据（属性）
//   2. 当数据变化时通过 C# event 主动通知外部
//   3. 持有业务逻辑（数据如何计算/变更）
//
// Model 的禁忌：
//   - 不持有任何 View 或 Controller 的引用
//   - 不调用任何 UnityEngine.UI 或渲染相关 API
//
// 用法示例：
//   public class GameProgressModel : ModelBase
//   {
//       public event Action<float> OnProgressChanged;
//
//       private float _progress;
//       public float Progress
//       {
//           get => _progress;
//           set { _progress = value; OnProgressChanged?.Invoke(value); }
//       }
//   }
public abstract class ModelBase { }
