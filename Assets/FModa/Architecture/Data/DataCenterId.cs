using System;
using System.Collections.Generic;
using UnityEngine;

namespace FModa.Data
{
    /// <summary>
    /// 数据中心 id 的位布局约定。
    ///
    /// long 共 64 位：
    ///   bit 63      符号位，恒为 0 —— 合成 id 永远非负，-1 哨兵不会被生成
    ///   bit 62..51  数据中心 Code，12 位，0 ~ 4095
    ///   bit 50..0   本地 id，51 位，1 ~ 2,251,799,813,685,247
    ///
    /// Code = 0 表示未分区：合成 id 等于本地 id，与旧版本逐位一致。
    ///
    /// 一旦某个 Code 被用于存档或网络传输，就不能再修改位布局、复用该 Code，
    /// 也不能给同一个 Code 换绑到另一种数据中心类型。
    /// </summary>
    public static class DataCenterId
    {
        /// <summary>数据中心 Code 占用的位数。</summary>
        public const int CenterBits = 12;

        /// <summary>本地 id 占用的位数。</summary>
        public const int LocalBits = 51;

        /// <summary>Code 的最大值（4095）。</summary>
        public const int MaxCenterCode = (1 << CenterBits) - 1;

        /// <summary>本地 id 的最大值（2^51 - 1）。</summary>
        public const long MaxLocalId = (1L << LocalBits) - 1;

        public static bool IsValidCenterCode(int centerCode)
            => centerCode >= 0 && centerCode <= MaxCenterCode;

        /// <summary>合法本地 id 从 1 开始，0 和负数都表示无效。</summary>
        public static bool IsValidLocalId(long localId)
            => localId > 0 && localId <= MaxLocalId;

        /// <summary>把 CenterCode 与本地 id 合成一个跨中心唯一的 long。</summary>
        public static long Compose(int centerCode, long localId)
        {
            if (!IsValidCenterCode(centerCode))
            {
                throw new ArgumentOutOfRangeException(nameof(centerCode), centerCode,
                    $"数据中心 Code 必须在 0 ~ {MaxCenterCode} 之间");
            }

            if (!IsValidLocalId(localId))
            {
                throw new ArgumentOutOfRangeException(nameof(localId), localId,
                    $"本地 id 必须在 1 ~ {MaxLocalId} 之间");
            }

            return ((long)centerCode << LocalBits) | localId;
        }

        /// <summary>取出合成 id 的本地部分。</summary>
        public static long Local(long id) => id & MaxLocalId;

        /// <summary>取出合成 id 的数据中心部分。负数（如 -1）返回 -1。</summary>
        public static int Center(long id) => (int)(id >> LocalBits);

        /// <summary>id 是否属于指定数据中心。负数一律不属于。</summary>
        public static bool BelongsTo(long id, int centerCode)
            => id >= 0 && Center(id) == centerCode;

        /// <summary>用于日志的可读描述，例如 "Code=1, Local=3"。</summary>
        public static string Describe(long id)
            => id < 0 ? $"Invalid({id})" : $"Code={Center(id)}, Local={Local(id)}";
    }

    /// <summary>
    /// 进程内 Code 占用表。
    /// 同一个非零 Code 被两种不同的数据中心类型使用时直接报错，
    /// 避免两个中心发出同一段 id 却互不知情。
    /// </summary>
    internal static class DataCenterCodeRegistry
    {
        private static readonly Dictionary<int, Type> owners = new();
        private static readonly HashSet<Type> warned = new();

        public static void Claim(int centerCode, Type centerType)
        {
            if (centerCode == 0) return;

            if (owners.TryGetValue(centerCode, out var existing) && existing != centerType)
            {
                Debug.LogError(
                    $"[DataCenter] Code {centerCode} 已被 {existing.Name} 占用，" +
                    $"{centerType.Name} 不能复用同一个 Code");
                return;
            }

            owners[centerCode] = centerType;
        }

        /// <summary>Editor 下提示：这个中心没有在 DataManager.DataCenterCodes 中占号。</summary>
        public static void WarnIfUnregistered(Type centerType)
        {
            if (!warned.Add(centerType)) return;

            Debug.LogWarning(
                $"[DataCenter] {centerType.Name} 未分配 Code（当前为 0），" +
                "其 id 不做跨中心隔离。请在 DataManager.DataCenterCodes 中占号。");
        }
    }
}
