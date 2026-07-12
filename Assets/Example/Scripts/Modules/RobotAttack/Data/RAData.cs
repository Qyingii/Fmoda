using UnityEngine;

namespace FModa.RA
{
    /// <summary>
    /// 机器人实体数据。
    /// ID 由 DataCenter 内部管理，不保存在此类中。
    /// </summary>
    public class RAData:IData
    {
        public GameObject robot;     // 关联的 GameObject 实例
        public string name;          // 显示名称
        public int maxHp;            // 最大血量
        public int hp;               // 当前血量
        public int attack;           // 攻击力

        public bool IsAlive => hp > 0;
    }
}
