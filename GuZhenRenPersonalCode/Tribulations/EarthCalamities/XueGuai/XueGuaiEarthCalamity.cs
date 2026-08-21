// ============================================================================
// 中文维护说明
// 文件职责：定义一项地灾的出现条件、危险度与战斗生命周期；对应本地化名称“雪怪之灾”。
// 主要类型：XueGuaiEarthCalamity。
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

namespace GuZhenRen.Tribulations.EarthCalamities.XueGuai;

public sealed class XueGuaiEarthCalamity :
    ITribulationDefinition,
    ITribulationCombatLifecycle,
    ITribulationTurnLifecycle,
    ITribulationDamageObserver,
    ITribulationCombatModifier
{
    private static readonly string Loss = Key("leader_hp_loss_this_turn");
    private static readonly string Nurture = Key("blood_nurture");
    private static readonly string NextAttack = Key("next_attack_bonus");

    public string Id => TribulationIds.XueGuai;
    public TribulationTier Tier => TribulationTier.EarthCalamity;
    public TribulationDanger Danger => TribulationDanger.Dangerous;
    public int BaseWeight => 1;

    public bool CanAppear(in TribulationSelectionContext context) => true;
    public float GetEnemyCompatibilityMultiplier(in TribulationSelectionContext context) => 1f;

    public Task OnAppliedAsync(TribulationContext context) =>
        EarthCalamitySupport.ApplyAnchorPowerAsync<XueGuaiPower>(context);

    public Task OnDamageResolvedAsync(
        TribulationContext context,
        TribulationDamageEvent damage)
    {
        if (ReferenceEquals(damage.Target, context.Leader) && damage.HpDamage > 0m)
        {
            TribulationStateStore.AddCounter(
                context,
                Loss,
                (int)damage.HpDamage,
                0,
                int.MaxValue);
        }
        if (ReferenceEquals(damage.Dealer, context.Leader) &&
            damage.IsAttack)
        {
            TribulationStateStore.SetFlag(context, NextAttack, false);
        }
        return Task.CompletedTask;
    }

    public async Task OnPlayerTurnEndAsync(TribulationContext context, int turn)
    {
        int loss = TribulationStateStore.GetCounter(context, Loss);
        int nurture = TribulationStateStore.GetCounter(context, Nurture);
        int tenPercent = EarthCalamitySupport.PercentCeiling(
            context.Leader,
            0.10m);
        int twentyPercent = EarthCalamitySupport.PercentCeiling(
            context.Leader,
            0.20m);
        decimal healPercent = loss >= twentyPercent
            ? 0m
            : loss >= tenPercent
                ? 0.05m
                : 0.10m;

        if (healPercent > 0m)
        {
            healPercent += nurture * 0.02m;
            await EarthCalamitySupport.HealAsync(
                context.Leader,
                EarthCalamitySupport.PercentCeiling(context.Leader, healPercent));
        }

        if (loss >= twentyPercent)
        {
            TribulationStateStore.AddCounter(context, Nurture, -1, 0, 3);
        }
        else if (loss < tenPercent)
        {
            if (nurture >= 3)
            {
                await EarthCalamitySupport.GainBlockAsync(
                    context.Leader,
                    EarthCalamitySupport.PercentCeiling(context.Leader, 0.15m));
                TribulationStateStore.SetFlag(context, NextAttack, true);
            }
            else
            {
                TribulationStateStore.SetCounter(context, Nurture, nurture + 1);
            }
        }

        TribulationStateStore.SetCounter(context, Loss, 0);
        EarthCalamitySupport.RefreshAnchorPower<XueGuaiPower>(context);
    }

    public Task OnEnemyTurnEndAsync(TribulationContext context, int round)
    {
        TribulationStateStore.SetFlag(context, NextAttack, false);
        return Task.CompletedTask;
    }

    public decimal ModifyDamageAdditive(
        TribulationContext context,
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay) =>
        ReferenceEquals(dealer, context.Leader) &&
        props.IsPoweredAttack() &&
        TribulationStateStore.GetFlag(context, NextAttack)
            ? EarthCalamitySupport.ScaleFlat(context, 8)
            : 0m;

    private static string Key(string local) =>
        TribulationStateStore.Key(TribulationIds.XueGuai, local);
}
