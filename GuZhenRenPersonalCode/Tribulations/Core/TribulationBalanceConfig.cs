// ============================================================================
// 中文维护说明
// 文件职责：实现灾劫系统的领域模型、注册表、平衡配置与生成流程。
// 主要类型：TribulationBalanceConfig。
// 实现要点：公开成员构成该模块的稳定协作面；修改签名时应同步检查注册点与调用方。
// 维护约定：灾劫选择先持久化再应用；新增钩子时同步更新事件路由和幂等标记。
// ============================================================================
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
