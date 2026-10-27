using Conquer.Unit;
using FModa.InputSystem;
using FModa.Multilingual;
using TMPro;
using UnityEngine;

/// <summary>
/// 单位血量面板 —— 显示所有机器人的当前 HP。
///
/// 演示 UIBase 的标准用法：
///   1. OnBind 中订阅单位模块的事件
///   2. OnUnbind 中取消订阅
///   3. OnRefresh 中做初始同步
/// </summary>
public class UnitHpPanel : UIBase
{
    [SerializeField] private TextMeshProUGUI _text;

    //单位模块
    private IUnitManager unitManager;
    private IInputManager inputManager;
    private IMultilingualManager multilingualManager;

    protected override void OnBind()
    {
        //获取单位模块
        unitManager = GF.GetModule<UnitManager>();
        inputManager = GF.GetModule<InputManager>();
        multilingualManager = GF.GetModule<MultilingualManager>();
        unitManager.OnHpChanged += RefreshAll;
    }

    protected override void OnUnbind()
    {
        unitManager.OnHpChanged -= RefreshAll;
    }

    protected override void OnRefresh()
    {
        RefreshAll();
    }

    void Update()
    {
        if (inputManager.KeyDown("OpenUI"))
        {
            GF.ui.OpenUI("BlankPanel");
        }

        if (inputManager.KeyDown("Spawn"))
        {
            //随机位置派生unitPoolPrefab
            unitManager.SpawnRandomUnit();
        }

        if (inputManager.KeyDown("EndFlow"))
        {
            unitManager.EndFlow();
        }
    }

    // ── 刷新显示 ──

    private void RefreshAll()
    {
        if (_text == null) return;

        var all = unitManager.GetAllUnitData();
        // "按下 Q 键扣血，按下W 键向前移动，按下E键打开测试UI，按下R键派生单位，按下空格键结束本流程\n";
        string pre=multilingualManager.GetText("ui.unitHpPanel.introduce")+'\n';
        string result = "";
        foreach (var kv in all)
        {
            var data = kv.Value;
            //$"序列号：{kv.Key}  血量：{data.hp} / {data.maxHp}  速度：{data.moveSpeed}\n";
            result += multilingualManager.GetText("ui.unitHpPanel.unitInfo", kv.Key, data.hp, data.maxHp, data.moveSpeed) + '\n';
        }

        _text.text =pre + (string.IsNullOrEmpty(result) ? multilingualManager.GetText("ui.unitHpPanel.noUnit") : result.TrimEnd('\n'));
    }
}
