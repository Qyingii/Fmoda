using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 泛型数据表 —— 适合低频/配置类数据。高频热数据请实现 <see cref="IDataCenter"/> 创建 typed 子类。
///
/// 使用示例：
/// <code>
/// int id = DataManager.Generic.Insert(new ConfigData { ... });
/// var data = DataManager.Generic.Get&lt;ConfigData&gt;(id);
/// DataManager.Generic.Update&lt;ConfigData&gt;(id, d => d.value = newValue);
/// DataManager.Generic.Remove&lt;ConfigData&gt;(id);
/// </code>
/// </summary>
public class GenericTable : IDataCenter
{
    private int _nextId = 1;

    // ── 存储结构：数据类型 → { entityId → 实例 } ──
    private readonly Dictionary<Type, Dictionary<int, object>> _db = new();

    // ── 可选索引：数据类型 → GameObject → entityId（避免 O(n) 遍历）──
    private readonly Dictionary<Type, Dictionary<GameObject, int>> _goIndex = new();

    // ── 泛型事件（注意：所有类型共用，只适合低频场景）──

    /// <summary> 新数据插入时触发 (type, id) </summary>
    public event Action<Type, int> OnInserted;

    /// <summary> 数据通过 Update 修改时触发 (type, id) </summary>
    public event Action<Type, int> OnChanged;

    /// <summary> 数据被移除时触发 (type, id) </summary>
    public event Action<Type, int> OnRemoved;

    // ── 核心 CRUD ──

    /// <summary> 插入一条数据，返回自动分配的 ID </summary>
    public int Insert<T>(T data) where T : class
    {
        if (data == null) throw new ArgumentNullException(nameof(data));

        int id = _nextId++;
        GetTable(typeof(T))[id] = data;
        OnInserted?.Invoke(typeof(T), id);
        return id;
    }

    /// <summary> 根据 ID 获取数据，不存在返回 null </summary>
    public T Get<T>(int id) where T : class
    {
        var table = GetTable(typeof(T));
        return table.TryGetValue(id, out var obj) ? obj as T : null;
    }

    /// <summary> 尝试获取数据 </summary>
    public bool TryGet<T>(int id, out T data) where T : class
    {
        var table = GetTable(typeof(T));
        if (table.TryGetValue(id, out var obj) && obj is T t)
        {
            data = t;
            return true;
        }
        data = null;
        return false;
    }

    /// <summary> 安全修改数据：读取 → 修改 → 触发 OnChanged </summary>
    public void Update<T>(int id, Action<T> modifier) where T : class
    {
        var table = GetTable(typeof(T));
        if (!table.TryGetValue(id, out var obj) || obj is not T data) return;

        modifier(data);
        OnChanged?.Invoke(typeof(T), id);
    }

    /// <summary> 移除指定数据，返回是否成功 </summary>
    public bool Remove<T>(int id) where T : class
    {
        var table = GetTable(typeof(T));
        if (!table.Remove(id)) return false;

        var type = typeof(T);
        if (_goIndex.TryGetValue(type, out var index))
        {
            var deadKeys = index.Where(kv => kv.Value == id).Select(kv => kv.Key).ToList();
            foreach (var k in deadKeys) index.Remove(k);
        }

        OnRemoved?.Invoke(type, id);
        return true;
    }

    /// <summary> 遍历某类型的所有数据 </summary>
    public IEnumerable<KeyValuePair<int, T>> GetAll<T>() where T : class
    {
        var table = GetTable(typeof(T));
        foreach (var kv in table)
        {
            if (kv.Value is T data)
                yield return new KeyValuePair<int, T>(kv.Key, data);
        }
    }

    /// <summary> 判断某 ID 是否存在 </summary>
    public bool Contains<T>(int id) where T : class
    {
        return GetTable(typeof(T)).ContainsKey(id);
    }

    // ── GameObject 索引（按需使用）──

    /// <summary> 注册 GameObject → entityId 的映射 </summary>
    public void BindGameObject<T>(int id, GameObject go) where T : class
    {
        if (go == null) return;
        var type = typeof(T);
        if (!_goIndex.ContainsKey(type))
            _goIndex[type] = new Dictionary<GameObject, int>();
        _goIndex[type][go] = id;
    }

    /// <summary> 通过 GameObject 查找 entityId（未找到返回 -1）</summary>
    public int GetIdByGameObject<T>(GameObject go) where T : class
    {
        var type = typeof(T);
        if (_goIndex.TryGetValue(type, out var index) && index.TryGetValue(go, out var id))
            return id;
        return -1;
    }

    /// <summary> 通过 GameObject 查找数据 </summary>
    public T GetByGameObject<T>(GameObject go) where T : class
    {
        int id = GetIdByGameObject<T>(go);
        return id < 0 ? null : Get<T>(id);
    }

    // ── 批量操作 ──

    /// <summary> 清空某类型的所有数据 </summary>
    public void Clear<T>() where T : class
    {
        var type = typeof(T);
        _db.Remove(type);
        _goIndex.Remove(type);
    }

    /// <summary> 清空全部数据 </summary>
    public void ClearAll()
    {
        _db.Clear();
        _goIndex.Clear();
    }

    // ── 内部 ──

    private Dictionary<int, object> GetTable(Type type)
    {
        if (!_db.TryGetValue(type, out var table))
        {
            table = new Dictionary<int, object>();
            _db[type] = table;
        }
        return table;
    }
}
