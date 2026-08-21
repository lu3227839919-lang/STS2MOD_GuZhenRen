// ============================================================================
// 中文维护说明
// 文件职责：定义一项地灾的出现条件、危险度与战斗生命周期；对应本地化名称“春晓翠鹂之灾”。
// 主要类型：ChunXiaoCuiLiEarthCalamity。
// 实现要点：灾劫能力按所实现的细粒度接口由事件路由选择性分派。
// 维护约定：灾劫选择先持久化再应用；新增钩子时同步更新事件路由和幂等标记。
// ============================================================================
using GuZhenRen.Tribulations.Contracts;
using GuZhenRen.Tribulations.Core;
using GuZhenRen.Tribulations.Runtime;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace GuZhenRen.Tribulations.EarthCalamities.ChunXiaoCuiLi;

public sealed class ChunXiaoCuiLiEarthCalamity :
    ITribulationDefinition,
    ITribulationCombatLifecycle,
    ITribulationTurnLifecycle,
    ITribulationDamageObserver,
    ITribulationCombatModifier,
    ITribulationPhaseController
{
    private static readonly string Spring = Key("spring_growth");
    private static readonly string PhaseTwo = Key("phase_two");
    private static readonly string FirstHitReduction = Key("first_hit_reduction");
    private static readonly string Ramp = Key("phase_two_attack_ramp_percent");

    public string Id => TribulationIds.ChunXiaoCuiLi;
    public TribulationTier Tier => TribulationTier.EarthCalamity;
    public TribulationDanger Danger => TribulationDanger.Aberrant;
    public int BaseWeight => 1;
    public string GetCurrentPhaseId(TribulationContext context) =>
        TribulationStateStore.GetFlag(context, PhaseTwo)
            ? "giant_calamity_burning_wood"
            : "spring_dawn";

    public bool CanAppear(in TribulationSelectionContext context) => true;
    public float GetEnemyCompatibilityMultiplier(in TribulationSelectionContext context) => 1f;

    public Task OnAppliedAsync(TribulationContext context) =>
        EarthCalamitySupport.ApplyAnchorPowerAsync<ChunXiaoCuiLiPower>(context);

    public async Task OnDamageResolvedAsync(
        TribulationContext context,
        TribulationDamageEvent damage)
    {
        if (ReferenceEquals(damage.Target, context.Leader))
            await EvaluatePhaseTransitionAsync(context);
    }

    public async Task EvaluatePhaseTransitionAsync(TribulationContext context)
    {
        if (TribulationStateStore.GetFlag(context, PhaseTwo) ||
            context.Leader.CurrentHp >= context.Leader.MaxHp * 0.55m)
            return;

        TribulationStateStore.SetFlag(context, PhaseTwo, true);
        TribulationStateStore.SetFlag(context, FirstHitReduction, false);
        int spring = TribulationStateStore.GetCounter(context, Spring);
        await EarthCalamitySupport.GainBlockAsync(
            context.Leader,
            EarthCalamitySupport.ScaleFlat(context, spring * 3));
        EarthCalamitySupport.RefreshAnchorPower<ChunXiaoCuiLiPower>(context);
    }

    public async Task OnEnemyTurnEndAsync(TribulationContext context, int round)
    {
        await EvaluatePhaseTransitionAsync(context);
        if (TribulationStateStore.GetFlag(context, PhaseTwo))
        {
            TribulationStateStore.AddCounter(context, Ramp, 3, 0, 999);
            return;
        }

        int spring = TribulationStateStore.GetCounter(context, Spring);
        await EarthCalamitySupport.HealAsync(
            context.Leader,
            EarthCalamitySupport.PercentCeiling(
                context.Leader,
                0.07m + spring * 0.01m));
        await EarthCalamitySupport.GainBlockAsync(
            context.Leader,
            EarthCalamitySupport.ScaleFlat(context, 10));
        TribulationStateStore.SetCounter(context, Spring, Math.Min(5, spring + 1));
        TribulationStateStore.SetFlag(context, FirstHitReduction, true);
        EarthCalamitySupport.RefreshAnchorPower<ChunXiaoCuiLiPower>(context);
    }

    public decimal ModifyDamageMultiplicative(
        TribulationContext context,
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        bool phaseTwo = TribulationStateStore.GetFlag(context, PhaseTwo);
        if (ReferenceEquals(target, context.Leader))
        {
            if (phaseTwo)
                return 1.10m;
            if (dealer?.IsPlayer == true &&
                (cardPlay != null || cardSource == null) &&
                TribulationStateStore.GetFlag(context, FirstHitReduction))
            {
                TribulationStateStore.SetFlag(context, FirstHitReduction, false);
                return 0.70m;
            }
        }

        if (phaseTwo &&
            ReferenceEquals(dealer, context.Leader) &&
            props.IsPoweredAttack())
        {
            int spring = TribulationStateStore.GetCounter(context, Spring);
            int ramp = TribulationStateStore.GetCounter(context, Ramp);
            return 1.20m + spring * 0.03m + ramp / 100m;
        }
        return 1m;
    }

    private static string Key(string local) =>
        TribulationStateStore.Key(TribulationIds.ChunXiaoCuiLi, local);
}
