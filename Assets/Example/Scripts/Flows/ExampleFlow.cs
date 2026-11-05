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

        //用事件来实现流程控制
        unitManager.OnFlowEnd += ()=>
        {
            GF.flow.SwitchFlow<EndFlow>(this);
        };

        //初始化
        // 1. 派生单位
        var go1 = GF.Spawn(GF.sgo.unitPrefab, new Vector3(-1, -1, 0), Quaternion.identity);

        // 2. 获取 Units
        var unit1 = go1.GetComponent<Units>();

        // 3. 写入 UnitDataCenter，并把 id 注入视图（注册的唯一写入者是 Flow）
        var unitId = unitManager.Register(new UnitData { maxHp = 90, hp = 90, moveSpeed = 2 });
        if (unitId > 0)
            unit1.BindUnit(unitId);

        //获取UnitHpPanel，触发一次界面更新
        var unitHpPanel = GF.sgo.gameObjects["UnitHpPanel"].GetComponent<UnitHpPanel>();
        unitHpPanel.RefreshAll();
    }

    public void Exit()
    {
        GF.RemoveModule<UnitManager>();//移除模块
        Debug.Log("退出 ExampleFlow");
    }
}
