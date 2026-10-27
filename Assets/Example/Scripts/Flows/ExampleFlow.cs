using System.Collections;
using System.Collections.Generic;
using Conquer.Unit;
using FModa.InputSystem;
using FModa.Multilingual;
using UnityEngine;

public class ExampleFlow : IFlow
{
    private IUnitManager unitManager;
    public void Enter()
    {
        Debug.Log("进入 ExampleFlow");

        //多语言是全局基础服务：尽早创建并初始化。
        //初始化时会读取玩家上次的选择；首次启动则按系统语言自动匹配。
        var multilingual = GF.GetModule<MultilingualManager>();
        multilingual.Init();

        //激活模块
        unitManager=GF.GetModule<UnitManager>();
        GF.CreateModule<InputManager>();

        //初始化模块
        unitManager.Init();

        //用事件来实现流程控制
        unitManager.OnFlowEnd += ()=>
        {
            GF.flow.SwitchFlow<EndFlow>(this);
        };
    }

    public void Exit()
    {
        GF.RemoveModule<UnitManager>();//移除模块
        Debug.Log("退出 ExampleFlow");
    }
}
