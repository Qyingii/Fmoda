public abstract class IData
{
    /// <summary>
    /// 唯一标识
    /// </summary>
    protected long id=-1;

    /// <summary>
    /// 唯一标识
    /// </summary>
    public long Id { get;}

    /// <summary>
    /// 设置唯一标识
    /// </summary>
    /// <param name="id"></param>
    public void SetId(long id)
    {
        this.id = id;
    }
}