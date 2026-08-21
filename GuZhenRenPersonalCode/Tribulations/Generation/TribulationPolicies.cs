using GuZhenRen.Aperture;
using GuZhenRen.Multiplayer;
using GuZhenRen.Tribulations.Contracts;
using GuZhenRen.Tribulations.Core;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Random;
using STS2RitsuLib;

namespace GuZhenRen.Tribulations.Generation;

public interface ITribulationTriggerPolicy
{
    bool ShouldTrigger(in TribulationSelectionContext context, Rng rng);
}

public interface ITribulationWeightResolver
{
    IReadOnlyDictionary<TribulationTier, float> ResolveTierWeights(
        in TribulationSelectionContext context,
        TribulationBalanceConfig config
    );

    float ResolveDefinitionWeight(
        ITribulationDefinition definition,
        in TribulationSelectionContext context,
        ApertureRunData data,
        TribulationBalanceConfig config
    );
}

public interface ITribulationHistoryPolicy
{
    void ApplyTierHistory(
        Dictionary<TribulationTier, float> weights,
        ApertureRunData data,
        TribulationBalanceConfig config
    );

    void RecordSelection(ApertureRunData data, TribulationSelection selection);
}

public interface ITribulationLeaderSelector
{
    Creature? SelectLeader(IEnumerable<Creature> enemies);
}

public interface ITribulationHealthScaler
{
    float GetMaxHpMultiplier(TribulationDanger danger, int currentRank, TribulationBalanceConfig config);
}

public sealed class TribulationTriggerPolicy(TribulationBalanceConfig config)
    : ITribulationTriggerPolicy
{
    public bool ShouldTrigger(in TribulationSelectionContext context, Rng rng)
    {
        if (context.Stage == TribulationProgressStage.Complete)
            return false;

        return config.TriggerChanceByRank.TryGetValue(context.Rank, out float chance) &&
               chance > 0f && rng.NextFloat() < chance;
    }
}

public sealed class TribulationWeightResolver : ITribulationWeightResolver
{
    public IReadOnlyDictionary<TribulationTier, float> ResolveTierWeights(
        in TribulationSelectionContext context,
        TribulationBalanceConfig config)
    {
        Dictionary<TribulationTier, float> weights = [];
        switch (context.Rank)
        {
            case 6:
                weights[TribulationTier.EarthCalamity] = 100f;
                break;
            case 7:
                (float earth7, float heaven7) = context.Stage switch
                {
                    TribulationProgressStage.Early => (65f, 35f),
                    TribulationProgressStage.Mid => (50f, 50f),
                    _ => (40f, 60f),
                };
                weights[TribulationTier.EarthCalamity] = earth7;
                weights[TribulationTier.HeavenlyTribulation] = heaven7;
                break;
            case 8 when config.EnableThousandTribulation:
                (float earth8t, float heaven8t, float thousand8t, float grand8t) = context.Stage switch
                {
                    TribulationProgressStage.Early => (35f, 35f, 20f, 10f),
                    TribulationProgressStage.Mid => (25f, 30f, 25f, 20f),
                    _ => (15f, 25f, 25f, 35f),
                };
                weights[TribulationTier.EarthCalamity] = earth8t;
                weights[TribulationTier.HeavenlyTribulation] = heaven8t;
                weights[TribulationTier.ThousandTribulation] = thousand8t;
                weights[TribulationTier.GrandTribulation] = grand8t;
                break;
            case 8:
                (float earth8, float heaven8, float grand8) = context.Stage switch
                {
                    TribulationProgressStage.Early => (40f, 40f, 20f),
                    TribulationProgressStage.Mid => (30f, 40f, 30f),
                    _ => (20f, 35f, 45f),
                };
                weights[TribulationTier.EarthCalamity] = earth8;
                weights[TribulationTier.HeavenlyTribulation] = heaven8;
                weights[TribulationTier.GrandTribulation] = grand8;
                break;
        }
        return weights;
    }

    public float ResolveDefinitionWeight(
        ITribulationDefinition definition,
        in TribulationSelectionContext context,
        ApertureRunData data,
        TribulationBalanceConfig config)
    {
        float weight = definition.BaseWeight;
        int recentIndex = data.RecentTribulationIds.IndexOf(definition.Id);
        if (recentIndex >= 0 && recentIndex < config.RecentRepeatMultipliers.Count)
            weight *= config.RecentRepeatMultipliers[recentIndex];

        float compatibility = Math.Clamp(
            definition.GetEnemyCompatibilityMultiplier(context),
            0.80f,
            1.20f
        );
        return weight * compatibility;
    }
}

public sealed class TribulationHistoryPolicy : ITribulationHistoryPolicy
{
    public void ApplyTierHistory(
        Dictionary<TribulationTier, float> weights,
        ApertureRunData data,
        TribulationBalanceConfig config)
    {
        if (data.SameTierStreak >= 2 && data.RecentTribulationTiers.Count > 0)
        {
            TribulationTier recent = (TribulationTier)data.RecentTribulationTiers[0];
            if (weights.ContainsKey(recent))
                weights[recent] *= config.SameTierPenalty;
        }

        if (weights.Count == 0)
            return;

        TribulationTier highest = weights.Keys.Max();
        if (data.HighestTierDryStreak == 3)
        {
            weights[highest] *= 2f;
        }
        else if (data.HighestTierDryStreak >= 4)
        {
            weights[highest] *= 4f;

            // 第五次抽取至少落在当前可用池的最高两级之一。
            TribulationTier[] ordered = weights.Keys.OrderByDescending(static x => x).ToArray();
            if (ordered.Length > 2)
            {
                HashSet<TribulationTier> allowed = [ordered[0], ordered[1]];
                foreach (TribulationTier tier in weights.Keys.ToArray())
                {
                    if (!allowed.Contains(tier)) weights[tier] = 0f;
                }
            }
        }
    }

    public void RecordSelection(ApertureRunData data, TribulationSelection selection)
    {
        int previousTier = data.RecentTribulationTiers.Count > 0
            ? data.RecentTribulationTiers[0]
            : (int)TribulationTier.None;

        data.SameTierStreak = previousTier == (int)selection.Tier
            ? data.SameTierStreak + 1
            : 1;

        TribulationTier highest = selection.CurrentRank switch
        {
            6 => TribulationTier.EarthCalamity,
            7 => TribulationTier.HeavenlyTribulation,
            8 => TribulationTier.GrandTribulation,
            _ => TribulationTier.None,
        };
        data.HighestTierDryStreak = selection.Tier == highest
            ? 0
            : data.HighestTierDryStreak + 1;

        data.RecentTribulationIds.Insert(0, selection.TribulationId);
        if (data.RecentTribulationIds.Count > 3)
            data.RecentTribulationIds.RemoveRange(3, data.RecentTribulationIds.Count - 3);

        data.RecentTribulationTiers.Insert(0, (int)selection.Tier);
        if (data.RecentTribulationTiers.Count > 3)
            data.RecentTribulationTiers.RemoveRange(3, data.RecentTribulationTiers.Count - 3);
    }
}

public sealed class TribulationLeaderSelector : ITribulationLeaderSelector
{
    public Creature? SelectLeader(IEnumerable<Creature> enemies)
    {
        return GuZhenRenDeterminism
            .OrderCreatures(enemies)
            .Where(IsEligibleLeader)
            .OrderByDescending(GetOriginalMaxHp)
            .ThenBy(c => c.CombatId ?? uint.MaxValue)
            .FirstOrDefault();
    }

    private static bool IsEligibleLeader(Creature creature) =>
        creature.IsMonster && creature.CurrentHp > 0 && creature.IsHittable;

    private static int GetOriginalMaxHp(Creature creature) =>
        creature.MonsterMaxHpBeforeModification ?? creature.MaxHp;
}

public sealed class TribulationHealthScaler : ITribulationHealthScaler
{
    public float GetMaxHpMultiplier(
        TribulationDanger danger,
        int currentRank,
        TribulationBalanceConfig config)
    {
        float dangerBonus = danger switch
        {
            TribulationDanger.Common => 0.40f,
            TribulationDanger.Dangerous => 0.55f,
            TribulationDanger.Aberrant => 0.70f,
            _ => 0f,
        };
        float rankBonus = config.RankHpBonus.TryGetValue(currentRank, out float bonus)
            ? bonus
            : 0f;
        return 1f + dangerBonus + rankBonus;
    }
}
