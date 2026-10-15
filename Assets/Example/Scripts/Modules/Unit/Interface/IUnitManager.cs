using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Conquer.Unit
{
        ///  <summary>
        ///  单位控制
        ///  </summary>
    public interface IUnitManager
    {
        Type InterfaceType { get; }

        int Priority { get; }

        public Action OnFlowEnd{get;set;} //流程结束时触发，ExampleFlow 订阅此事件后切换流程
        public Action OnHpChanged{get;set;} //血量变化时触发，UnitHpPanel 订阅此事件后刷新 UI

        /// <summary>
        /// 单位移动
        /// </summary>
        /// <param name="unit"></param>
        /// <param name="id"></param>
        public Vector3 Move(long id);

        /// <summary>
        /// 初始化
        /// </summary>
        public void Init();

        /// <summary>
        /// 单位扣血
        /// </summary>
        /// <param name="id"></param>
        public void TakeDamage(long id);

        /// <summary>
        /// 结束流程
        /// </summary>
        public void EndFlow();

        /// <summary>
        /// 获取全部单位数据
        /// </summary>
        /// <returns></returns>
        public Dictionary<long, UnitData> GetAllUnitData();

        /// <summary>
        /// 注册数据
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public long Register(UnitData data);

        /// <summary>
        /// 注销数据
        /// </summary>
        /// <param name="id"></param>
        public void UnRegister(long id);

    }
}
