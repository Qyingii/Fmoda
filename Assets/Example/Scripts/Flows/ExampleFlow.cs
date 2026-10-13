using System.Collections;
using System.Collections.Generic;
using Conquer.Unit;
using UnityEngine;

public class ExampleFlow : IFlow
{
    private IUnitManager unitManager;
    public void Enter()
    {
        Debug.Log("进入 ExampleFlow");
        //激活模块
        unitManager=GF.GetModule<UnitManager>();

        //初始化模块
        unitManager.Init();

        //用事件来实现流程控制
        unitManager.OnFlowEnd += ()=>
        {
            GF.flowManager.SwitchFlow<EndFlow>(this);
        };
    }

    public void Exit()
    {
        GF.RemoveModule<UnitManager>();//移除模块
        Debug.Log("退出 ExampleFlow");
    }
}
