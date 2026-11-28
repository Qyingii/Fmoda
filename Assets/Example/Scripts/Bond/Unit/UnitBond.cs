using FModa.Bond;
using FModa.SeedRandom;
using UnityEngine;

namespace Conquer.Unit
{
    /// <summary>
    /// 本粘合剂主要负责单位与单位渲染、UI之间的粘合
    /// </summary>
    public class UnitBond : IBond
    {
        UnitManager unit;
        public UnitBond()
        {
            unit = GF.GetModule<UnitManager>();
        }

        /// <summary>
        /// 派生单位
        /// </summary>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        /// <param name="parent"></param>
        /// <returns></returns>
        public Units SpawnUnit(Vector3 position= default, Quaternion rotation= default, Transform parent= null)
        {
            // 1. 派生单位
            var go = GF.Spawn(GF.sgo.unitPrefab, position, rotation, parent);

            // 2. 获取 Units
            var unit1 = go.GetComponent<Units>();

            // 3. 写入 UnitDataCenter，并把 id 注入视图（注册的唯一写入者是 Flow）
            var unitId = unit.Register(new UnitData { maxHp = 90, hp = 90, moveSpeed = 2 });
            if (unitId > 0)
                unit1.BindUnit(unitId);

            //获取UnitHpPanel，触发一次界面更新
            var unitHpPanel = GF.sgo.gameObjects["UnitHpPanel"].GetComponent<UnitHpPanel>();
            unitHpPanel.RefreshAll();

            return unit1;
        }

        /// <summary>
        /// 获取兜底数据
        /// </summary>
        /// <returns></returns>
        public long CreateUnitData()
        {
            // 兜底：场景里静态摆放、没有生成方注入 id 的单位，在这里自注册一次。
            // 由 Flow 注入的单位（unitId > 0）不会走到这里，因此不会重复注册。
            return unit.Register(new UnitData
            {
                maxHp = 100,
                hp = 100,
                moveSpeed = 3,
            });
        }

        /// <summary>
        /// 随机位置派生unitPoolPrefab
        /// </summary>
        /// <returns></returns>
        public GameObject SpawnRandomunitPoolPrefab()
        {
            var random = GF.GetModule<SeedRandomManager>();
            return GF.Spawn(GF.sgo.unitPoolPrefab, new Vector3(random.NextInt(-5, 5), random.NextInt(-5, 5), 0), Quaternion.identity);
        }
    }
}
