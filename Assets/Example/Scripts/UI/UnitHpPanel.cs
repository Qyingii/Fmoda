using FModa.RA;
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

    // ── UIWidget ──

    protected override void OnBind()
    {
        var dc = DataManager.Combat;
        dc.OnHpChanged += OnHpChanged;
        dc.OnEntityRemoved += OnEntityRemoved;
    }

    protected override void OnUnbind()
    {
        var dc = DataManager.Combat;
        dc.OnHpChanged -= OnHpChanged;
        dc.OnEntityRemoved -= OnEntityRemoved;
    }

    protected override void OnRefresh()
    {
        RefreshAll();
    }

    // ── 事件回调 ──

    private void OnHpChanged(int entityId, int oldHp, int newHp)
    {
        RefreshAll();
    }

    private void OnEntityRemoved(int entityId)
    {
        RefreshAll();
    }

    // ── 刷新显示 ──

    private void RefreshAll()
    {
        if (_text == null) return;

        var all = DataManager.Combat.GetAllEntities();
        string result = "";
        foreach (var kv in all)
        {
            var data = kv.Value;
            result += $"{data.name}  HP {data.hp} / {data.maxHp}\n";
        }

        _text.text = string.IsNullOrEmpty(result) ? "无存活单位" : result.TrimEnd('\n');
    }
}
