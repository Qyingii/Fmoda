using System;
using FModa.Data;

namespace FModa.SeedRandom
{
    /// <summary>
    /// 带种子的随机数模块。
    /// 设置种子后使用 System.Random 生成可复现序列；未设置时回退到 UnityEngine.Random。
    /// 所有区间均为左闭右开，浮点上界额外处理舍入误差，保证不会返回 maxExclusive。
    /// </summary>
    public class SeedRandomManager : ISeedRandomManager, IModule
    {
        // 小于 1 的最大 float，避免 NextDouble 或 Unity 随机值经转换后变成 1。
        private const float MaxUnitFloat = 0.99999994f;

        private System.Random _random;
        private SeedRandomDataCenter Data => DataManager.SeedRandom;

        public Type InterfaceType => typeof(ISeedRandomManager);

        /// <summary>无逻辑帧任务，使用默认模块优先级。</summary>
        public int Priority => 5;

        public bool HasSeed => Data.GetState().hasSeed;

        public int? CurrentSeed
        {
            get
            {
                var state = Data.GetState();
                return state.hasSeed ? state.seed : (int?)null;
            }
        }

        public void SetSeed(int seed)
        {
            Data.SetSeed(seed);
            _random = new System.Random(seed);
        }

        public void ClearSeed()
        {
            Data.ClearSeed();
            _random = null;
        }

        public int NextInt()
        {
            return _random?.Next() ?? UnityEngine.Random.Range(0, int.MaxValue);
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            ValidateRange(minInclusive, maxExclusive);
            if (minInclusive == maxExclusive) return minInclusive;

            return _random?.Next(minInclusive, maxExclusive)
                   ?? UnityEngine.Random.Range(minInclusive, maxExclusive);
        }

        public int NextInt(int seed, int minInclusive, int maxExclusive)
        {
            ValidateRange(minInclusive, maxExclusive);
            if (minInclusive == maxExclusive) return minInclusive;

            return new System.Random(seed).Next(minInclusive, maxExclusive);
        }

        public float NextFloat()
        {
            return NextUnitFloat(_random);
        }

        public float NextFloat(float minInclusive, float maxExclusive)
        {
            return NextFloatInternal(_random, minInclusive, maxExclusive);
        }

        public float NextFloat(int seed, float minInclusive, float maxExclusive)
        {
            return NextFloatInternal(new System.Random(seed), minInclusive, maxExclusive);
        }

        public void OnRemove()
        {
            _random = null;
            Data.Clear();
        }

        private static void ValidateRange(int minInclusive, int maxExclusive)
        {
            if (minInclusive > maxExclusive)
            {
                throw new ArgumentException(
                    $"随机区间下界不能大于上界：[{minInclusive}, {maxExclusive})",
                    nameof(minInclusive));
            }
        }

        private static float NextFloatInternal(
            System.Random random,
            float minInclusive,
            float maxExclusive)
        {
            ValidateRange(minInclusive, maxExclusive);
            if (minInclusive == maxExclusive) return minInclusive;

            // 使用 double 计算，避免 max - min 在 float 中溢出或损失过多精度。
            double sample = NextUnitFloat(random);
            double value = minInclusive + (maxExclusive - (double)minInclusive) * sample;
            float result = (float)value;

            if (result < minInclusive) return minInclusive;
            if (result >= maxExclusive) return PreviousFloat(maxExclusive, minInclusive);
            return result;
        }

        private static float NextUnitFloat(System.Random random)
        {
            if (random != null)
                return Math.Min((float)random.NextDouble(), MaxUnitFloat);

            // UnityEngine.Random.value 可能返回 1，这里统一收敛到 [0, 1)。
            return Math.Min(UnityEngine.Random.value, MaxUnitFloat);
        }

        private static void ValidateRange(float minInclusive, float maxExclusive)
        {
            if (float.IsNaN(minInclusive) || float.IsNaN(maxExclusive) ||
                float.IsInfinity(minInclusive) || float.IsInfinity(maxExclusive))
            {
                throw new ArgumentException("随机区间边界必须是有限数值，不能为 NaN 或 Infinity。");
            }

            if (minInclusive > maxExclusive)
            {
                throw new ArgumentException(
                    $"随机区间下界不能大于上界：[{minInclusive}, {maxExclusive})",
                    nameof(minInclusive));
            }
        }

        /// <summary>
        /// 返回 maxExclusive 的下一个更小 float；仅用于修正区间计算的舍入碰撞。
        /// </summary>
        private static float PreviousFloat(float maxExclusive, float minInclusive)
        {
            float previous;
            if (maxExclusive == 0f)
            {
                previous = -float.Epsilon;
            }
            else
            {
                int bits = BitConverter.ToInt32(BitConverter.GetBytes(maxExclusive), 0);
                bits = maxExclusive > 0f ? bits - 1 : bits + 1;
                previous = BitConverter.ToSingle(BitConverter.GetBytes(bits), 0);
            }

            // 对相邻的 float 边界，不存在位于两者之间的第三个 float，此时返回下界。
            return previous < minInclusive ? minInclusive : previous;
        }
    }
}
