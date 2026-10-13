
using UnityEngine;

public class FlowManager
{
    public IFlow CurrentFlow { get; private set; }//当前流程
    /// <summary>
    /// 开始的第一个流程
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public void StartFlow<T>() where T : IFlow, new()
    {
        var flow = new T();
        flow.Enter();
        CurrentFlow = flow;//记录当前流程

        Debug.Log("当前流程：" + CurrentFlow.GetType().Name);
    }

    /// <summary>
    /// 切换流程
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="currentFlow"></param>
    public void SwitchFlow<T>(IFlow currentFlow) where T : IFlow, new()
    {
        currentFlow.Exit();
        var flow = new T();
        flow.Enter();
        CurrentFlow = flow;//记录当前流程

        //清理数据
        DataManager.ClearAll();

        Debug.Log("当前流程：" + CurrentFlow.GetType().Name);
    }
}
