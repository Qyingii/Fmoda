
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class FlowManager
{
    public IFlow CurrentFlow { get; private set; }//当前流程

    //记录切换流程数据
    Type type;
    List<object> list;

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

    /// <summary>
    /// 切换流程
    /// </summary>
    public void SwitchFlow(Type flowType, List<object> list = null)
    {
        if (!typeof(IFlow).IsAssignableFrom(flowType))
        {
            Debug.LogError($"{flowType} 没有实现 IFlow");
            return;
        }

        CurrentFlow.Exit();

        IFlow flow = Activator.CreateInstance(flowType) as IFlow;
        flow.Enter(list);
        CurrentFlow = flow;//记录当前流程
        Debug.Log($"切换流程 {flowType.Name}");
    }

    /// <summary>
    /// 切换场景并切换流程
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="sceneName"></param>
    /// <param name="currentFlow"></param>
    /// <param name="list"></param>
    public void SwitchSceneAndFlow<T>(string sceneName,IFlow currentFlow, List<object> list = null) where T : IFlow, new()
    {
        //切换场景
        SceneManager.LoadScene(sceneName);
        SceneManager.sceneLoaded+= ScanScene;

        type=typeof(T);
        this.list=list;
    }

    /// <summary>
    /// 切换场景并切换流程
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="index"></param>
    /// <param name="currentFlow"></param>
    /// <param name="list"></param>
    public void SwitchSceneAndFlow<T>(int index,IFlow currentFlow, List<object> list = null) where T : IFlow, new()
    {
        //切换场景
        SceneManager.LoadScene(index);
        SceneManager.sceneLoaded+= ScanScene;

        type=typeof(T);
        this.list=list;
    }

    /// <summary>
    /// 切换场景并切换流程
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="sceneName"></param>
    /// <param name="currentFlow"></param>
    /// <param name="list"></param>
    public void SwitchSceneAndFlowAsync<T>(string sceneName,IFlow currentFlow, List<object> list = null) where T : IFlow, new()
    {
        //切换场景
        SceneManager.LoadSceneAsync(sceneName);
        SceneManager.sceneLoaded+= ScanScene;

        type=typeof(T);
        this.list=list;
    }

    /// <summary>
    /// 切换场景并切换流程
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="index"></param>
    /// <param name="currentFlow"></param>
    /// <param name="list"></param>
    public void SwitchSceneAndFlowAsync<T>(int index,IFlow currentFlow, List<object> list = null) where T : IFlow, new()
    {
        //切换场景
        SceneManager.LoadSceneAsync(index);
        SceneManager.sceneLoaded+= ScanScene;

        type=typeof(T);
        this.list=list;
    }

    //切换流程
    private IEnumerator SwitchFlow()
    {
        //等待1帧，先让其他数据加载完
        yield return new WaitForSeconds(1);
        SwitchFlow(type, list);
        type = null;
        list = null;
    }

    private void ScanScene(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= ScanScene;
        GF.Instance.StartCoroutine(SwitchFlow());
    }
}
