namespace FModa.SeedRandom
{
    /// <summary>
    /// 带种子的随机数模块对外契约。
    /// 所有区间均为左闭右开 [minInclusive, maxExclusive)。
    /// </summary>
    public interface ISeedRandomManager
    {
        /// <summary>是否已通过 <see cref="SetSeed"/> 设置当前种子。</summary>
        bool HasSeed { get; }

        /// <summary>当前种子；未设置时返回 null。</summary>
        int? CurrentSeed { get; }

        /// <summary>
        /// 设置当前种子并重新开始一条可复现的随机序列。
        /// 再次设置相同种子会得到相同序列。
        /// </summary>
        void SetSeed(int seed);

        /// <summary>清除当前种子，之后的随机调用回退到 UnityEngine.Random。</summary>
        void ClearSeed();

        /// <summary>返回 [0, int.MaxValue) 内的整数。</summary>
        int NextInt();

        /// <summary>返回 [minInclusive, maxExclusive) 内的整数。</summary>
        int NextInt(int minInclusive, int maxExclusive);

        /// <summary>
        /// 使用独立种子返回 [minInclusive, maxExclusive) 内的整数。
        /// 相同参数始终得到相同结果，且不读取或修改当前种子及其随机序列。
        /// </summary>
        int NextInt(int seed, int minInclusive, int maxExclusive);

        /// <summary>返回 [0, 1) 内的浮点数。</summary>
        float NextFloat();

        /// <summary>返回 [minInclusive, maxExclusive) 内的浮点数。</summary>
        float NextFloat(float minInclusive, float maxExclusive);

        /// <summary>
        /// 使用独立种子返回 [minInclusive, maxExclusive) 内的浮点数。
        /// 相同参数始终得到相同结果，且不读取或修改当前种子及其随机序列。
        /// </summary>
        float NextFloat(int seed, float minInclusive, float maxExclusive);
    }
}
