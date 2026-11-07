using System.Collections.Generic;

public interface IFlow
{

    /// <summary>
    /// 流程进入时调用
    /// </summary>
    /// <param name="list"></param>
    public void Enter(List<object> list=null);

    /// <summary>
    /// 流程更新时调用
    /// </summary>
    public void Update();

    /// <summary>
    /// 流程退出时调用
    /// </summary>
    public void Exit();
}
