using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 轻量实体基类 —— 统一管理标签和实体关系。
///
/// 继承此基类可获得标签系统，也可直接使用 MonoBehaviour 作为实体（标签非必需）。
/// 组件请使用 MonoBehaviour + Data 模式，通过 AddComponent&lt;T&gt;() 注册。
/// </summary>
public class GameEntity : MonoBehaviour
{
    [Header("标签")]
    [SerializeField, Tooltip("逗号分隔，如 Enemy,Ground")]
    private string _tagString;

    private readonly HashSet<string> _tags = new HashSet<string>();

    public bool HasTag(string tag)       => _tags.Contains(tag);
    public bool AddTag(string tag)       => _tags.Add(tag);
    public bool RemoveTag(string tag)    => _tags.Remove(tag);

    // ── 实体关系 ────────────────────────────────────────────────────────────────

    private GameEntity _parentEntity;
    public GameEntity ParentEntity
    {
        get => _parentEntity;
        set => _parentEntity = value;
    }

    // ── Unity 生命周期 ─────────────────────────────────────────────────────────

    protected virtual void Awake()
    {
        if (string.IsNullOrEmpty(_tagString)) return;
        foreach (var t in _tagString.Split(','))
        {
            var trimmed = t.Trim();
            if (!string.IsNullOrEmpty(trimmed))
                _tags.Add(trimmed);
        }
    }

    // ── 子实体 ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// 获取所有子实体（每次遍历场景，不缓存）
    /// </summary>
    public List<GameEntity> GetAllChildEntities()
    {
        var children = new List<GameEntity>();
        var found = GetComponentsInChildren<GameEntity>();
        foreach (var child in found)
        {
            if (child == this) continue;
            children.Add(child);
            child.ParentEntity = child.transform.parent?.GetComponentInParent<GameEntity>();
        }
        return children;
    }
}
