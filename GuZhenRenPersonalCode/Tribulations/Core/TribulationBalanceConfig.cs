namespace GuZhenRen.Tribulations.Core;

public sealed class TribulationBalanceConfig
{
    public bool EnableThousandTribulation { get; init; }

    public IReadOnlyDictionary<int, float> TriggerChanceByRank { get; init; } =
        new Dictionary<int, float>
        {
            [6] = 0.40f,
            [7] = 0.50f,
            [8] = 0.60f,
            [9] = 0.00f,
        };

    public IReadOnlyDictionary<TribulationDanger, int> DangerBaseWeights { get; init; } =
        new Dictionary<TribulationDanger, int>
        {
            [TribulationDanger.Common] = 10,
            [TribulationDanger.Dangerous] = 6,
            [TribulationDanger.Aberrant] = 3,
        };

    public IReadOnlyList<float> RecentRepeatMultipliers { get; init; } =
        [0.10f, 0.40f, 0.70f];

    public float SameTierPenalty { get; init; } = 0.35f;

    public IReadOnlyDictionary<int, float> RankNumericScaling { get; init; } =
        new Dictionary<int, float> { [6] = 1f, [7] = 1.20f, [8] = 1.45f, [9] = 1.70f };

    public IReadOnlyDictionary<int, float> RankHpBonus { get; init; } =
        new Dictionary<int, float> { [6] = 0f, [7] = 0.15f, [8] = 0.30f, [9] = 0.45f };

    public static TribulationBalanceConfig Default { get; } = new();
}
