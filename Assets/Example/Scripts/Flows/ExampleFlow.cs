using System.Collections.Generic;
using Conquer.Unit;
using FModa.InputSystem;
using FModa.Multilingual;
using UnityEngine;

public class ExampleFlow : IFlow
{
    private UnitBond unitBond;
    private SwitchFlowBond switchFlowBond;
    public void Enter(IFlow lastFlow,List<object> list=null)
    {
        GF.Log("进入 ExampleFlow");
        //激活模块，模块会自动初始化
        GF.CreateModule<MultilingualManager>();
        GF.CreateModule<UnitManager>();
        GF.CreateModule<InputManager>();

        unitBond = GF.GetBond<UnitBond>();
        switchFlowBond = GF.GetBond<SwitchFlowBond>();

        //事件绑定
        switchFlowBond.OnFlowEnd += ()=>
        {
            GF.flow.SwitchFlow<EndFlow>(this);
        };

        //流程实际内容
        unitBond.SpawnUnit(new Vector3(-1, -1, 0), Quaternion.identity);
    }

    public void Exit()
    {
        GF.RemoveModule<UnitManager>();//移除模块
        GF.Log("退出 ExampleFlow");
    }

    public void Update()
    {
        
    }
}
