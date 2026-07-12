using System;
using System.Collections.Generic;
using UnityEngine;

namespace FModa.RA
{
    /// <summary>
    /// 战斗数据中心 —— 实现 <see cref="IDataCenter"/> 的 typed 子类。
    ///
    /// 为什么不用泛型 GenericTable？
    ///   战斗数据是高频变更的热数据，如果放在 GenericTable 中，
    ///   OnChanged 事件会伴随每次 hp 修改触发，而 GenericTable 的事件是所有类型共享的，
    ///   订阅者必须做类型过滤。实现 IDataCenter 后事件是独立的，订阅者零过滤开销。
    ///
    /// 这个类的写法就是"如何创建一个 typed 数据中心"的标准样板：
    ///   - 实现 IDataCenter 接口
    ///   - 私有 Dictionary 存储
    ///   - 私有 _nextId 管理 ID 序列
    ///   - public typed 事件
    ///   - 增删改查方法用显式的 TryGetValue + 事件触发（不用基类技巧，更清晰）
    /// </summary>
    public class CombatDataCenter : IDataCenter
    {
        // ── 存储 ──
        private readonly Dictionary<int, RAData> _entities = new();
        private readonly Dictionary<GameObject, int> _goIndex = new();
        private int _nextId = 1;

        // ── typed 事件（只跟战斗数据相关，没有类型过滤开销）──

        /// <summary> HP 变化时触发 (entityId, oldHp, newHp) </summary>
        public event Action<int, int, int> OnHpChanged;

        /// <summary> 实体被移除时触发 (entityId) </summary>
        public event Action<int> OnEntityRemoved;

        // ── typed API ──

        /// <summary> 添加实体，返回自动分配的 ID </summary>
        public int AddEntity(RAData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            int id = _nextId++;
            _entities[id] = data;
            if (data.robot != null)
                _goIndex[data.robot] = id;

            return id;
        }

        /// <summary> 通过实体 ID 获取数据 </summary>
        public RAData GetEntity(int id)
        {
            _entities.TryGetValue(id, out var data);
            return data;
        }

        /// <summary> 通过 GameObject 获取数据（O(1)）</summary>
        public RAData GetByRobot(GameObject go)
        {
            if (go == null || !_goIndex.TryGetValue(go, out var id))
                return null;
            return GetEntity(id);
        }

        /// <summary> 通过 GameObject 获取实体 ID（-1 表示未找到）</summary>
        public int GetIdByRobot(GameObject go)
        {
            if (go != null && _goIndex.TryGetValue(go, out var id))
                return id;
            return -1;
        }

        /// <summary>
        /// 设置 HP，触发 OnHpChanged（不含伤害公式，仅为数据写入）。
        /// </summary>
        public void SetHp(int entityId, int newHp)
        {
            if (!_entities.TryGetValue(entityId, out var data)) return;
            int oldHp = data.hp;
            data.hp = newHp;
            OnHpChanged?.Invoke(entityId, oldHp, newHp);
        }

        /// <summary>
        /// 移除实体，自动清理 GameObject 索引。
        /// </summary>
        public void RemoveEntity(int entityId)
        {
            if (!_entities.TryGetValue(entityId, out var data)) return;
            if (data.robot != null) _goIndex.Remove(data.robot);
            _entities.Remove(entityId);
            OnEntityRemoved?.Invoke(entityId);
        }

        /// <summary> 遍历所有实体 </summary>
        public IEnumerable<KeyValuePair<int, RAData>> GetAllEntities()
        {
            foreach (var kv in _entities)
                yield return kv;
        }

        /// <summary> 清空所有战斗数据 </summary>
        public void Clear()
        {
            _entities.Clear();
            _goIndex.Clear();
        }
    }
}
