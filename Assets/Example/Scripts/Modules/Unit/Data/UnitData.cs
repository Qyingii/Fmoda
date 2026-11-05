using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Conquer.Unit
{
    /// <summary>
    /// 单位数据
    /// </summary>
    [System.Serializable]
    public class UnitData : IData
    {
        //最大生命值
        public int maxHp;
        //当前生命值
        public int hp;
        //移动速度
        public float moveSpeed;

        public override IData Clone()
        {
            return new UnitData
            {
                maxHp = maxHp,
                hp = hp,
                moveSpeed = moveSpeed,
            };
        }
    }
}

