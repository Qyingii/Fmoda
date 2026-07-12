
public class FlowManager
{
    /// <summary>
    /// 开始的第一个流程
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public void StartFlow<T>() where T : FlowBase, new()
    {
        var flow = new T();
        flow.Enter();
    }

    /// <summary>
    /// 切换流程
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="currentFlow"></param>
    public void SwitchFlow<T>(FlowBase currentFlow) where T : FlowBase, new()
    {
        currentFlow.Exit();
        var flow = new T();
        flow.Enter();
    }
}
