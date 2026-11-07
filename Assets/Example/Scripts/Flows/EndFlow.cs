using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EndFlow : IFlow
{
    public void Enter(List<object> list = null)
    {
        Debug.Log("进入 EndFlow,这是最后一个流程，即将退出游戏");
    }

    public void Exit()
    {
    }

    public void Update()
    {
    }
}
