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
    /// 曲线 5、6、7、8、9、11、12、13、15；
    /// 六转与九转为境界质变，各额外增加 1 点。
    /// </summary>
    private static readonly IReadOnlyDictionary<int, int>
        YuanQiCapacityByRank = new Dictionary<int, int>
        {
            [1] = 5,
            [2] = 6,
            [3] = 7,
            [4] = 8,
            [5] = 9,
            [6] = 11,
            [7] = 12,
            [8] = 13,
            [9] = 15,
        };

    /// <summary>
    /// 每回合元气回复（首回合固定发放 5 点，与转数无关）。
    /// 一至二转 2、三至五转 3、六至七转 4、八至九转 5。
    /// 九转回复不设超过 6 点，避免普通蛊与合练蛊难以消耗完元气。
    /// </summary>
    public static int GetYuanQiRecovery(int rank)
    {
        return Math.Max(MinimumRank, rank) switch
        {
            <= 2 => 2,
            <= 5 => 3,
            <= 7 => 4,
            _ => 5,
        };
    }

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
