using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EndFlow : IFlow
{
    public void Enter()
    {
        Debug.Log("进入 EndFlow,这是最后一个流程，即将退出游戏");
    }
}
