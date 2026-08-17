namespace GuZhenRen.Aperture;

/// <summary>
/// 暂不包含灾劫的空窍/仙窍升级状态机。
///
/// 一至五转沿用普通/精英/首领战的修为收益；
/// 五转修为满后直接进入六转；
/// 六至八转每次战斗胜利获得 1 点仙窍进度；
/// 九转为当前实现上限。
/// </summary>
public static class ApertureProgression
{
    public const int MinimumRank = 1;
    public const int ImmortalRank = 6;
    public const int MaximumImplementedRank = 9;

    /// <summary>
    /// 空窍三转解锁“杀招推演”（0.8.0 杀招系统）。
    /// </summary>
    public const int ShaZhaoDerivationUnlockRank = 3;

    /// <summary>
    /// 八至九转每场最多推演两次；第一次成功后补发第二张推演牌。
    /// </summary>
    public const int ShaZhaoDerivationSecondRank = 8;

    public const int ShaZhaoDerivationMaxPerCombat = 2;

    private static readonly IReadOnlyDictionary<int, int> RequiredXpByRank =
        new Dictionary<int, int>
        {
            [1] = 1,
            [2] = 2,
            [3] = 3,
            [4] = 4,
            [5] = 5,
            [6] = 2,
            [7] = 2,
            [8] = 3,
            [9] = 0,
        };

    /// <summary>
    /// 空窍转数对应的元气容量上限。
    /// 曲线 3、4、4、5、5、7、7、8、9。
    /// </summary>
    private static readonly IReadOnlyDictionary<int, int>
        YuanQiCapacityByRank = new Dictionary<int, int>
        {
            [1] = 3,
            [2] = 4,
            [3] = 4,
            [4] = 5,
            [5] = 5,
            [6] = 7,
            [7] = 7,
            [8] = 8,
            [9] = 9,
        };

    /// <summary>
    /// 每回合元气回复（首回合固定发放满额，与转数无关）。
    /// 回复量根据当前空窍元气上限决定：
    /// 上限 3 回复 1、上限 4~5 回复 2、上限 6~7 回复 3、
    /// 上限 8~9 回复 4。
    /// 对应一至九转曲线为 1、2、2、2、2、3、3、4、4。
    /// </summary>
    public static int GetYuanQiRecovery(int rank)
    {
        int capacity = GetYuanQiCapacity(rank);
        return capacity switch
        {
            <= 3 => 1,
            <= 5 => 2,
            <= 7 => 3,
            _ => 4,
        };
    }

    /// <summary>
    /// 每场战斗开始时的空窍元气量：默认补满至当前转数上限。
    /// </summary>
    public static int GetYuanQiStartAmount(int rank) =>
        GetYuanQiCapacity(rank);

    public static int GetRequiredXp(int rank)
    {
        return RequiredXpByRank.TryGetValue(rank, out int value)
            ? value
            : 0;
    }

    public static int GetMaxHpAward(int rank)
    {
        return rank switch
        {
            6 => 2,
            7 => 2,
            8 => 3,
            9 => 3,
            _ => 0,
        };
    }

    public static int GetYuanQiCapacity(int rank)
    {
        int normalizedRank = Math.Clamp(
            rank,
            MinimumRank,
            MaximumImplementedRank
        );
        return YuanQiCapacityByRank[normalizedRank];
    }

    /// <summary>
    /// 结算一场胜利提供的修为。
    /// 一场战斗最多提升一个转数，溢出修为保留到下一转。
    /// </summary>
    public static ApertureTransition GainVictoryXp(
        ApertureRunData data,
        int mortalXp
    )
    {
        ArgumentNullException.ThrowIfNull(data);
        data.Normalize();

        if (data.IsCultivationComplete)
        {
            return ApertureTransition.None(data.Rank);
        }

        int amount = data.Rank < ImmortalRank
            ? Math.Max(0, mortalXp)
            : 1;

        if (amount <= 0)
        {
            return ApertureTransition.None(data.Rank);
        }

        int previousRank = data.Rank;
        int requiredXp = GetRequiredXp(previousRank);
        data.Xp += amount;

        if (requiredXp <= 0 || data.Xp < requiredXp)
        {
            return ApertureTransition.Progress(previousRank);
        }

        int overflow = data.Xp - requiredXp;
        data.Rank++;

        // 五转进入仙窍，以及仙窍阶段之间的突破，
        // 原版都会从新阶段的 0 点进度开始。
        data.Xp = previousRank >= 5
            ? 0
            : overflow;

        if (data.Rank >= MaximumImplementedRank)
        {
            data.Rank = MaximumImplementedRank;
            data.Xp = 0;
            data.IsCultivationComplete = true;
        }

        return ApertureTransition.RankAdvanced(
            previousRank,
            data.Rank,
            data.IsCultivationComplete
        );
    }
}

public readonly record struct ApertureTransition(
    int PreviousRank,
    int CurrentRank,
    bool RankChanged,
    bool CultivationComplete
)
{
    public static ApertureTransition None(int rank) =>
        new(rank, rank, false, false);

    public static ApertureTransition Progress(int rank) =>
        new(rank, rank, false, false);

    public static ApertureTransition RankAdvanced(
        int previousRank,
        int currentRank,
        bool cultivationComplete
    ) =>
        new(
            previousRank,
            currentRank,
            true,
            cultivationComplete
        );
}
