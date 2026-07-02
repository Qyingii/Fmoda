using UnityEngine;

// 游戏单位的公共基类，持有所有实体共享的游戏字段，你可以在这里定制你需要的公共字段。
// 具体实体（EPlayer、ETurret、EAi 等）继承此类而非直接继承 EntityBehaviour。
public class UnitEntity : EntityBehaviour
{
    [Header("基础")]
    [SerializeField] protected string m_name;
    [SerializeField] protected string m_tag;
    [SerializeField] protected Sprite m_sprite;
    //这里可以拓展更多公共字段

    public string Name       { get => m_name;       set => m_name = value; }
    public Sprite Sprite
    {
        get => m_sprite;
        set
        {
            m_sprite = value;
            if (m_sprite != null)
            {
                var sr = GetComponent<SpriteRenderer>();
                if (sr != null) sr.sprite = m_sprite;
            }
        }
    }

    protected override void Awake()
    {
        if (m_sprite != null)
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null) sr.sprite = m_sprite;
        }
        InitTags(m_tag); // 将序列化字符串（如 "Enemy,Ground"）解析到 HashSet
        base.Awake();
    }
}
