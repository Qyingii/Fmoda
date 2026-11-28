using System.IO;
using FModa.Data;

namespace FModa.SeedRandom
{
    /// <summary>随机模块的种子状态数据中心。</summary>
    public class SeedRandomDataCenter : DataCenter<SeedRandomData>, IDataCenter
    {
        public SeedRandomDataCenter(int centerCode) : base(centerCode) { }

        /// <summary>获取唯一的种子状态；首次访问时创建。</summary>
        public SeedRandomData GetState()
        {
            foreach (var pair in datas)
                return pair.Value;

            var data = new SeedRandomData();
            AddData(data);
            return data;
        }

        public void SetSeed(int seed)
        {
            var data = GetState();
            data.hasSeed = true;
            data.seed = seed;
        }

        public void ClearSeed()
        {
            var data = GetState();
            data.hasSeed = false;
            data.seed = 0;
        }

        /// <summary>该中心的业务不变量是至多一条种子状态。</summary>
        protected override void RebuildAfterRestore()
        {
            if (datas.Count > 1)
                throw new InvalidDataException("随机种子数据中心最多只能包含一条状态");
        }

        /// <summary>追加种子前，当前候选中心必须还没有种子状态。</summary>
        protected override void ValidateAppendData(SeedRandomData data)
        {
            if (datas.Count > 0)
                throw new InvalidDataException("随机种子数据中心已有状态，不能继续追加第二条状态");
        }
    }
}
