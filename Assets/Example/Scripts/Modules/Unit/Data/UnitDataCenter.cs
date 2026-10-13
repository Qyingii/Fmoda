using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Conquer.Unit
{
    public class UnitDataCenter : IDataCenter
    {
        //单位数据
        private readonly Dictionary<int, UnitData> unitDatas = new();
        private int id = 0;

        //获取单位数据
        public UnitData GetUnitData(int unitId)
        {
            if(unitDatas.TryGetValue(unitId, out var unitData))
            {
                return unitData;
            }
            else
            {
                Debug.LogError($"未找到单位数据，unitId: {unitId}");
                return null;
            }
        }

        //获取全部单位数据
        public Dictionary<int, UnitData> GetAllUnitData()
        {
            return unitDatas;
        }

        //添加单位数据
        public int AddUnitData(UnitData unitData)
        {
            if(unitData == null) 
            {
                Debug.LogError("无法添加空的单位数据");
                return -1;
            }
            
            id++;
            unitDatas.Add(id, unitData);
            return id;
        }

        //更新单位数据
        public void UpdateUnitData(int unitId, UnitData unitData)
        {
            if(unitDatas.ContainsKey(unitId))
            {
                unitDatas[unitId] = unitData;
            }
            else
            {
                Debug.LogWarning($"未找到单位数据，unitId: {unitId}, 无法更新");
            }
        }

        //更新单位数据
        public void UpdateUnitData(int unitId, int maxHp=-1, int hp=-1, float moveSpeed=-1)
        {
            if(unitDatas.TryGetValue(unitId, out var unitData))
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

        //移除单位数据
        public void RemoveUnitData(int unitId)
        {
            if(unitDatas.ContainsKey(unitId))
            {
                unitDatas.Remove(unitId);
            }
            else
            {
                Debug.LogWarning($"未找到单位数据，unitId: {unitId}, 无法删除");
            }
        }

        //清空所有单位数据
        public void Clear()
        {
            unitDatas.Clear();
            id = 0;
        }
    }

}
