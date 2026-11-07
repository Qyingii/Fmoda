
using System;
using System.Collections.Generic;
using UnityEngine;

public class FlowManager
{
    public IFlow CurrentFlow { get; private set; }//当前流程

    /// <summary>
    /// 开始流程
    /// </summary>
    /// <param name="flowType"></param>
    public void StartFlow(Type flowType)
    {
        if (!typeof(IFlow).IsAssignableFrom(flowType))
        {
            Debug.LogError($"{flowType} 没有实现 IFlow");
            return;
        }

        IFlow flow = Activator.CreateInstance(flowType) as IFlow;
        flow.Enter();
        CurrentFlow = flow;//记录当前流程
        Debug.Log($"开始流程 {flowType.Name}"); 
    }
    
    /// <summary>
    /// 开始的第一个流程
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public void StartFlow<T>() where T : IFlow, new()
    {
        var flow = new T();
        flow.Enter();
        CurrentFlow = flow;//记录当前流程
        Debug.Log($"开始流程 {typeof(T).Name}");
    }

    /// <summary>
    /// 切换流程
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="currentFlow"></param>
    /// <param name="list">想传递给下一个流程的数据</param>
    public void SwitchFlow<T>(IFlow currentFlow,List<object> list=null) where T : IFlow, new()
    {
        currentFlow.Exit();
        var flow = new T();
        flow.Enter(list);
        CurrentFlow = flow;//记录当前流程

        Debug.Log($"切换流程 {typeof(T).Name}");
    }
}
