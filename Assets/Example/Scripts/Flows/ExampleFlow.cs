using System.Collections;
using System.Collections.Generic;
using FModa.RA;
using UnityEngine;

public class ExampleFlow : IFlow
{
    private IRA RAManager;
    public void Enter()
    {
        Debug.Log("进入 ExampleFlow");
        //激活模块
        RAManager=GF.GetModule<RAManager>();
        RAManager.OnFlowEnd += ()=>
        {
            GF.flowManager.SwitchFlow<EndFlow>(this);
        };
    }

    public void Exit()
    {
        Debug.Log("退出 ExampleFlow");
    }
}
