using System.Collections;
using System.Collections.Generic;
using FModa.Data;
using UnityEngine;

namespace Conquer.Unit
{
    public class UnitDataCenter : DataCenter<UnitData>,IDataCenter
    {
        /// <summary>
        /// 更新单位数据
        /// </summary>
        /// <param name="unitId"></param>
        /// <param name="maxHp"></param>
        /// <param name="hp"></param>
        /// <param name="moveSpeed"></param>
        public void UpdateUnitData(long unitId, int maxHp=-1, int hp=-1, float moveSpeed=-1)
        {
            if(datas.TryGetValue(unitId, out var unitData))
            {
                if(maxHp != -1) unitData.maxHp = maxHp;
                if(hp != -1) unitData.hp = hp;
                if(moveSpeed != -1) unitData.moveSpeed = moveSpeed;
            }
            else
            {
                Debug.LogWarning($"未找到单位数据，unitId: {unitId}, 无法更新");
            }
        }
    }

}
