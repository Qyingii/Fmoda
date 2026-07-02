using UnityEngine;

// Bootstrap：MVC 的装配入口，负责创建 Model 并分发给 View 和 Controller
//
// 使用方式：
//   1. 在场景中新建一个空 GameObject，挂载此脚本
//   2. 在 Inspector 中将 CounterView 和 CounterController 所在的 GameObject 拖入对应字段
//   3. 运行即可
//
// Bootstrap 是唯一同时持有三者引用的地方，其余各层互不感知。
public class CounterBootstrap : MonoBehaviour
{
    [SerializeField] private CounterView       _view;
    [SerializeField] private CounterController _controller;

    private void Awake()
    {
        var model = new CounterModel();
        _view.Bind(model);
        _controller.Init(model);
    }
}
