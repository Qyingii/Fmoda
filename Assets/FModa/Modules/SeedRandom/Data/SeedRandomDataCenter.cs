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
    }
}
