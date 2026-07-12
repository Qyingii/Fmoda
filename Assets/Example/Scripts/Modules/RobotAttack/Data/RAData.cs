using UnityEngine;

namespace ECMVC.RA
{
    /// <summary>
    /// 机器人实体数据 —— 由 DataCenter 统一管理。
    /// 字段全部 public 仅为序列化和编辑器可见，外部应通过 DataCenter API 读写。
    /// </summary>
    public class RAData
    {
        /// <summary> 唯一 ID，由 DataCenter.AddEntity 自动分配，分配后为只读 </summary>
        public int id;

        /// <summary> 关联的 GameObject 实例 </summary>
        public GameObject robot;

        /// <summary> 显示名称 </summary>
        public string name;

        /// <summary> 最大血量 </summary>
        public int maxHp;

        /// <summary> 当前血量 </summary>
        public int hp;

        /// <summary> 攻击力 </summary>
        public int attack;

        /// <summary> 是否存活 </summary>
        public bool IsAlive => hp > 0;
    }
}
