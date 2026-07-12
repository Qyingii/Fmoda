using System.Collections;
using System.Collections.Generic;
using ECMVC.RA;
using UnityEngine;

public class ExampleFlow : FlowBase
{
    private IRA RAManager;
    public void Enter()
    {
        Debug.Log("进入 ExampleFlow");
        //激活模块
        RAManager=GF.GetModule<RAManager>();
    }

    public void Exit()
    {
        Debug.Log("退出 ExampleFlow");
    }
}
