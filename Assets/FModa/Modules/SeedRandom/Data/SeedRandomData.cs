using System;

namespace FModa.SeedRandom
{
    /// <summary>
    /// 随机模块的种子状态。System.Random 的序列状态属于模块内部实现，不存入数据中心。
    /// </summary>
    [Serializable]
    public class SeedRandomData : IData
    {
        /// <summary>是否已经设置当前种子。</summary>
        public bool hasSeed;

        /// <summary>当前种子；hasSeed 为 false 时忽略此字段。</summary>
        public int seed;

        public override IData Clone()
        {
            return new SeedRandomData
            {
                hasSeed = hasSeed,
                seed = seed,
            };
        }
    }
}
