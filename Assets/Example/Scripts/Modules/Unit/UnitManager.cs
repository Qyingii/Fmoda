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

        //防止 OnDestroy 被 GF 与 Unity 各调用一次
        private bool _destroyed;

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

        /// <summary>
        /// 安全查询：id 失效或数据已被删除时返回 false，调用方据此停止操作。
        /// </summary>
        public bool TryGetUnitData(long id, out UnitData data)
        {
            return unitDataCenter.TryGetData(id, out data);
        }

        /// <summary>把合成 id 转成用于显示的本地序号。</summary>
        public long ToLocalId(long id)
        {
            return unitDataCenter.ToLocalId(id);
        }

#region 生命周期函数
        public void OnDestroy()
        {
            if (_destroyed) return;
            _destroyed = true;

            OnFlowEnd = null;
            OnHpChanged = null;
        }

        #endregion

        #region 流程

        //结束流程
        public void EndFlow()
        {
            //转流程时清除数据，让用户知道删除数据时不会有反向通知
            unitDataCenter.Clear();
            OnHpChanged?.Invoke(); // 数据已清空，通知 UI 重新取一次（此时为空）
            OnFlowEnd?.Invoke();
        }
#endregion
#region 单位操作

        //单位移动
        public Vector3 Move(long id)
        {
            if (!unitDataCenter.TryGetData(id, out var data))
                return Vector3.zero;

            return new Vector2(0, 1) * Time.deltaTime * data.moveSpeed;
        }

        //单位扣血
        public void TakeDamage(long id)
        {
            if (!unitDataCenter.TryGetData(id, out var data))
                return;

            unitDataCenter.UpdateUnitData(id, hp: data.hp - 10);
            OnHpChanged?.Invoke();
        }
#endregion

#region UI显示

        //获取全部单位数据
        public Dictionary<long, UnitData> GetAllUnitData()
        {
            return unitDataCenter.GetAllData();
        }

        #endregion
    }
}

