using Conquer.Unit;
using TMPro;
using UnityEngine;

/// <summary>
/// 单位血量面板 —— 显示所有机器人的当前 HP。
///
/// 演示 UIWidget 的标准用法：
///   1. OnBind 中订阅 CombatDataCenter 的事件
///   2. OnUnbind 中取消订阅
///   3. OnRefresh 中做初始同步
/// </summary>
public class UnitHpPanel : UIWidget
{
    [SerializeField] private TextMeshProUGUI _text;

    //单位模块
    private IUnitManager unitManager;
    // ── UIWidget ──

    protected override void OnBind()
    {
        //获取单位模块
        unitManager = GF.GetModule<UnitManager>();
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

    // ── 刷新显示 ──

    private void RefreshAll()
    {
        if (_text == null) return;

        var all = unitManager.GetAllUnitData();
        string pre="按下 Q 键扣血，按下W 键向前移动，按下空格键结束本流程\n";
        string result = "";
        foreach (var kv in all)
        {
            var data = kv.Value;
            result += $"序列号：{kv.Key}  血量：{data.hp} / {data.maxHp}  速度：{data.moveSpeed}\n";
        }

        _text.text =pre + (string.IsNullOrEmpty(result) ? "无存活单位" : result.TrimEnd('\n'));
    }
}
