// ============================================================================
// 中文维护说明
// 文件职责：实现灾劫系统的领域模型、注册表、平衡配置与生成流程。
// 主要类型：TribulationGenerator。
// 实现要点：随机选择使用玩家专属随机流，避免多人端因枚举顺序产生分歧。
// 实现补充：灾劫能力按所实现的细粒度接口由事件路由选择性分派。
// 维护约定：灾劫选择先持久化再应用；新增钩子时同步更新事件路由和幂等标记。
// ============================================================================
using GuZhenRen.Aperture;
using GuZhenRen.Tribulations.Contracts;
using GuZhenRen.Tribulations.Generation;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Random;
using STS2RitsuLib;

namespace GuZhenRen.Tribulations.Core;

public sealed class TribulationGenerator
{
    private const string TriggerRngDomain = "aperture/tribulation/trigger";
    private const string TierRngDomain = "aperture/tribulation/tier";
    private const string DefinitionRngDomain = "aperture/tribulation/definition";

    private readonly TribulationRegistry _registry;
    private readonly ITribulationTriggerPolicy _triggerPolicy;
    private readonly ITribulationWeightResolver _weightResolver;
    private readonly ITribulationHistoryPolicy _historyPolicy;
    private readonly ITribulationLeaderSelector _leaderSelector;
    private readonly ITribulationHealthScaler _healthScaler;
    private readonly TribulationBalanceConfig _config;

    public TribulationGenerator(
        TribulationRegistry registry,
        ITribulationTriggerPolicy triggerPolicy,
        ITribulationWeightResolver weightResolver,
        ITribulationHistoryPolicy historyPolicy,
        ITribulationLeaderSelector leaderSelector,
        ITribulationHealthScaler healthScaler,
        TribulationBalanceConfig config)
    {
        _registry = registry;
        _triggerPolicy = triggerPolicy;
        _weightResolver = weightResolver;
        _historyPolicy = historyPolicy;
        _leaderSelector = leaderSelector;
        _healthScaler = healthScaler;
        _config = config;
    }

    public TribulationSelection? TryGenerate(TribulationSelectionContext context)
    {
        if (context.Rank < ApertureProgression.ImmortalRank ||
            context.Stage == TribulationProgressStage.Complete)
            return null;

        Rng triggerRng = RitsuLibFramework.GetModPlayerRng(
            context.Player, Entry.ModId, TriggerRngDomain);
        if (!_triggerPolicy.ShouldTrigger(context, triggerRng))
            return null;

        Dictionary<TribulationTier, float> tierWeights =
            _weightResolver.ResolveTierWeights(context, _config)
                .ToDictionary(static pair => pair.Key, static pair => pair.Value);

        // Disabled/empty tiers are removed before tier RNG is consumed. This keeps
        // adding definitions inside one tier from altering other tier probabilities.
        foreach (TribulationTier tier in tierWeights.Keys.ToArray())
        {
            if (!_registry.GetByTier(tier).Any(d => d.CanAppear(context)))
                tierWeights.Remove(tier);
        }
        if (tierWeights.Count == 0)
            return null;

        _historyPolicy.ApplyTierHistory(tierWeights, context.RunData, _config);
        Rng tierRng = RitsuLibFramework.GetModPlayerRng(
            context.Player, Entry.ModId, TierRngDomain);
        TribulationTier selectedTier = WeightedPick(tierWeights, tierRng);

        ITribulationDefinition[] definitions = _registry
            .GetByTier(selectedTier)
            .Where(d => d.CanAppear(context))
            .OrderBy(d => d.Id, StringComparer.Ordinal)
            .ToArray();

        Dictionary<ITribulationDefinition, float> definitionWeights = definitions
            .ToDictionary(
                d => d,
                d => _weightResolver.ResolveDefinitionWeight(
                    d, context, context.RunData, _config));

        Rng definitionRng = RitsuLibFramework.GetModPlayerRng(
            context.Player, Entry.ModId, DefinitionRngDomain);
        ITribulationDefinition definition = WeightedPick(definitionWeights, definitionRng);

        Creature? leader = _leaderSelector.SelectLeader(context.Combat.Enemies);
        if (leader?.CombatId is not uint leaderId)
            return null;

        float multiplier = _healthScaler.GetMaxHpMultiplier(
            definition.Danger, context.Rank, _config);
        ulong tag = ((ulong)tierRng.NextUnsignedInt() << 32) |
                    definitionRng.NextUnsignedInt();

        return new TribulationSelection(
            definition.Id,
            definition.Tier,
            definition.Danger,
            leaderId,
            multiplier,
            context.Rank,
            context.Xp,
            context.Floor,
            tag);
    }

    private static T WeightedPick<T>(IReadOnlyDictionary<T, float> weights, Rng rng)
        where T : notnull
    {
        float total = weights.Values.Where(v => v > 0f).Sum();
        if (total <= 0f)
            throw new InvalidOperationException("Tribulation weight pool has no positive weight.");

        float roll = rng.NextFloat(total);
        T? last = default;
        foreach ((T item, float weight) in weights)
        {
            if (weight <= 0f) continue;
            last = item;
            roll -= weight;
            if (roll < 0f) return item;
        }
        return last!;
    }
}
