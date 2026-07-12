using System;
using System.Collections.Generic;
using ECMVC.RA;
using UnityEngine;

/// <summary>
/// 全局数据中心 —— 封装所有运行时数据，提供类型安全访问和变更事件。
/// 外部代码通过 <c>GF.dataCenter</c> 访问。
///
/// 使用示例：
/// <code>
/// int id = GF.dataCenter.AddEntity(new RAData { robot = go, name = "玩家", hp = 100, ... });
/// GF.dataCenter.OnHpChanged += (entityId, oldHp, newHp) => { ... };
/// var data = GF.dataCenter.GetEntityByRobot(go);
/// GF.dataCenter.SetEntityHp(id, 50);
/// </code>
/// </summary>
public class DataCenter
{
    // ── 内部存储 ──

    /// <summary> entityId → RAData </summary>
    private readonly Dictionary<int, RAData> _entityMap = new();

    /// <summary> GameObject → entityId 快速反查（避免 O(n) 遍历查找）</summary>
    private readonly Dictionary<GameObject, int> _robotToId = new();

    private int _nextId = 1;

    // ── 事件 ──

    /// <summary> HP 变化时触发 (entityId, oldHp, newHp) </summary>
    public event Action<int, int, int> OnHpChanged;

    /// <summary> 实体被移除时触发 (entityId) </summary>
    public event Action<int> OnEntityRemoved;

    // ── 公开 API ──

    /// <summary> 注册新实体，返回自动分配的唯一 entityId </summary>
    public int AddEntity(RAData data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));

        data.id = _nextId;
        _entityMap[_nextId] = data;
        if (data.robot != null)
            _robotToId[data.robot] = _nextId;

        Debug.Log($"[DataCenter] 注册实体 #{_nextId}: {data.name}");
        return _nextId++;
    }

    /// <summary> 通过 entityId 查找，不存在返回 null </summary>
    public RAData GetEntity(int entityId)
    {
        _entityMap.TryGetValue(entityId, out var data);
        return data;
    }

    /// <summary> 通过 GameObject 快速查找（O(1)），不存在返回 null </summary>
    public RAData GetEntityByRobot(GameObject robot)
    {
        if (robot == null || !_robotToId.TryGetValue(robot, out var id))
            return null;
        return GetEntity(id);
    }

    /// <summary> 获取所有存活实体的只读快照 </summary>
    public IReadOnlyCollection<RAData> GetAllEntities() => _entityMap.Values;

    /// <summary>
    /// 直接设置实体的 HP（不包含任何伤害/治疗公式，仅仅是数据写入）。
    /// 业务逻辑（伤害计算、死亡判定等）请放在对应的 System 中。
    /// </summary>
    /// <param name="entityId">目标实体 ID</param>
    /// <param name="newHp">要设置的新 HP 值</param>
    public void SetEntityHp(int entityId, int newHp)
    {
        if (!_entityMap.TryGetValue(entityId, out var data)) return;

        int oldHp = data.hp;
        data.hp = newHp;
        OnHpChanged?.Invoke(entityId, oldHp, newHp);
    }

    /// <summary> 移除实体（自动清理反查表）</summary>
    public void RemoveEntity(int entityId)
    {
        if (_entityMap.TryGetValue(entityId, out var data))
        {
            if (data.robot != null) _robotToId.Remove(data.robot);
            _entityMap.Remove(entityId);
            OnEntityRemoved?.Invoke(entityId);
            Debug.Log($"[DataCenter] 移除实体 #{entityId}: {data.name}");
        }
    }

    /// <summary> 清理所有数据（场景切换时调用）</summary>
    public void Clear()
    {
        _entityMap.Clear();
        _robotToId.Clear();
        _nextId = 1;
    }
}
