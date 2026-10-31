using System;
using System.Collections;
using System.Collections.Generic;
using FModa.Data;
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

        //具有对象池的单位预制体
        public GameObject unitPoolPrefab;

        /// <summary>
        /// 注册数据
        /// </summary>
        /// <returns></returns>
        public long Register(UnitData data)
        {
            return unitDataCenter.AddData(data);
        }

        /// <summary>
        /// 注销数据
        /// </summary>
        /// <param name="id"></param>
        public void UnRegister(long id)
        {
            unitDataCenter.RemoveData(id);
        }

#region 生命周期函数
        void OnDestroy()
        {
            OnFlowEnd = null;
            OnHpChanged = null;
        }

        #endregion

        #region 流程
        //初始化
        public void Init()
        {
            // 1. 派生单位
            var go1 = GF.Spawn(unitPrefab, new Vector3(-1, -1, 0), Quaternion.identity);

            // 2. 获取 Units
            var unit1 = go1.GetComponent<Units>();

            // 3. 写入 UnitDataCenter
            unit1.unitId = Register(new UnitData { maxHp = 90, hp = 90, moveSpeed = 2 });
        }

        //结束流程
        public void EndFlow()
        {
            //转流程时清除数据，让用户知道删除数据时不会有反向通知
            unitDataCenter.Clear(); 
            OnFlowEnd?.Invoke();
        }
#endregion
#region 单位操作

        //单位移动
        public Vector3 Move(long id)
        {
            return new Vector2(0, 1) * Time.deltaTime*unitDataCenter.GetData(id).moveSpeed;
        }

        //单位扣血
        public void TakeDamage(long id)
        {
            unitDataCenter.UpdateUnitData(id, hp: unitDataCenter.GetData(id).hp - 10);
            OnHpChanged?.Invoke();
        }
#endregion

#region UI显示

        //获取全部单位数据
        public Dictionary<long, UnitData> GetAllUnitData()
        {
            return unitDataCenter.GetAllData();
        }

        //随机派生单位
        public void SpawnRandomUnit()
        {
            GF.Spawn(unitPoolPrefab, new Vector3(UnityEngine.Random.Range(-5, 5), UnityEngine.Random.Range(-5, 5), 0), Quaternion.identity);
        }

        #endregion
    }
}

