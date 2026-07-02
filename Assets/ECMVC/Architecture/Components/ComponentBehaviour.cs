using UnityEngine;

public class ComponentBehaviour
{
    protected UnitEntity m_UnitEntity;//组件所属的实体对象
    protected GameObject m_EgameObject;//组件所属的游戏对象

    //属性
    public UnitEntity UnitEntity{ get => m_UnitEntity;}
    public GameObject EgameObject{ get => m_EgameObject;}

    //构造函数，需要至少提供使用组件的实体对象
    public ComponentBehaviour(UnitEntity entityBehaviour)
    {
        this.m_UnitEntity = entityBehaviour;
        this.m_EgameObject = entityBehaviour.gameObject;
    }

    //方法,生命周期函数方法已经用接口实现，只需要调用对应接口即可使用
    public virtual void Add(){}//初始化方法，在组件第一次添加到实体对象时调用
    public virtual void Remove(){}//销毁方法，在组件从实体对象移除时调用
}
