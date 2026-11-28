using System;
using FModa.Bond;

public class SwitchFlowBond : IBond
{
    /// <summary>
    /// 切换到结束流程
    /// </summary>
    public event Action OnFlowEnd;

    /// <summary>
    /// 切换到结束流程
    /// </summary>
    public void FlowEnd()
    {
        OnFlowEnd?.Invoke();

        //触发后清空事件，防止重复触发
        OnFlowEnd = null;
    }
}
