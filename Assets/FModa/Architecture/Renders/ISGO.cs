namespace FModa.GameObjectManager
{
    /// <summary>
    /// 场景游戏对象接口
    /// 用于标记指定的游戏对象被收录到GameObjectManager中，以保证其会被GameObjectManager管理
    /// </summary>
    public interface ISGO
    {
        /// <summary>
        /// 跨场景游戏对象id,用于被加载到指定的字段中，该值由用户手动填写，请确保每个场景内每个游戏对象唯一
        /// </summary>
        string SgoId { get;}

        /// <summary>
        /// 场景名称，用于标记该游戏对象应该在哪个场景中被加载，不写则默认加载
        /// </summary>
        string SceneName { get;}
    }
}
