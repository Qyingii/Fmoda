using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Conquer.Unit
{
    /// <summary>
    /// 单位控制
    /// </summary>
    public class UnitManager : MonoBehaviour,IModule,IUnitManager
    {
        public Type InterfaceType => typeof(IUnitManager);

        public int Priority => 5;

        public Action OnFlowEnd{get;set;} //流程结束时触发，ExampleFlow 订阅此事件后切换流程

        public Action OnHpChanged{get;set;}//血量变化时触发，UnitHpPanel 订阅此事件后刷新 UI
        private UnitDataCenter unitDataCenter => DataManager.Unit;//数据中心，用于存储战斗数据

        // 单位预制体
        public GameObject unitPrefab;

#region 生命周期函数
        void OnDestroy()
        {
            OnFlowEnd = null;
            OnHpChanged = null;
            unitDataCenter.Clear(); //清除数据
        }
#endregion

#region 流程
        //初始化
        public void Init()
        {
            // 1. 派生单位
            var go1 = Instantiate(unitPrefab, new Vector3(-1, -1, 0), Quaternion.identity);
            var go2 = Instantiate(unitPrefab, new Vector3(1, -1, 0), Quaternion.identity);

            // 2. 获取 Units
            var unit1 = go1.GetComponent<Units>();
            var unit2 = go2.GetComponent<Units>();

            // 3. 写入 UnitDataCenter
            unit1.SetId(unitDataCenter.AddUnitData(new UnitData { maxHp = 100, hp = 100, moveSpeed = 3 }));
            unit2.SetId(unitDataCenter.AddUnitData(new UnitData { maxHp = 90, hp = 90, moveSpeed = 2 }));
        }

        //结束流程
        public void EndFlow()
        {
            OnFlowEnd?.Invoke();
        }
#endregion
#region 单位操作

        //单位移动
        public Vector3 Move(int id)
        {
            return new Vector2(0, 1) * Time.deltaTime*unitDataCenter.GetUnitData(id).moveSpeed;
        }

        //单位扣血
        public void TakeDamage(int id)
        {
            unitDataCenter.UpdateUnitData(id, hp: unitDataCenter.GetUnitData(id).hp - 10);
            OnHpChanged?.Invoke();
        }
#endregion

#region UI显示

        //获取全部单位数据
        public Dictionary<int, UnitData> GetAllUnitData()
        {
            return unitDataCenter.GetAllUnitData();
        }

#endregion
    }
}

