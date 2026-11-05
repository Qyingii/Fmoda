public abstract class IData
{
    /// <summary>
    /// 唯一标识
    /// </summary>
    protected long id=-1;

    /// <summary>
    /// 唯一标识
    /// </summary>
    public long Id => id;

    /// <summary>
    /// 设置唯一标识。
    /// 仅供框架内部使用（DataCenter 负责发号与回收），外部不要调用。
    /// </summary>
    /// <param name="id"></param>
    internal void SetId(long id)
    {
        this.id = id;
    }

    /// <summary>
    /// 克隆数据
    /// </summary>
    /// <returns></returns>
    public abstract IData Clone();
}