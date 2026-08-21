// ============================================================================
// 中文维护说明
// 文件职责：定义一项地灾的出现条件、危险度与战斗生命周期；对应本地化名称“黑焰星坠”。
// 主要类型：HeiYanXingZhuiEarthCalamity。
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

namespace GuZhenRen.Tribulations.EarthCalamities.HeiYanXingZhui;

public sealed class HeiYanXingZhuiEarthCalamity :
    ITribulationDefinition,
    ITribulationCombatLifecycle,
    ITribulationTurnLifecycle,
    ITribulationDamageObserver,
    ITribulationCombatModifier
{
    private static readonly string Flame = Key("black_flame");
    private static readonly string PlayerLoss = Key("player_hp_loss_total");
    private static readonly string LeaderLoss = Key("leader_hp_loss_this_turn");
    private static readonly string Scar = Key("star_scar");

    public string Id => TribulationIds.HeiYanXingZhui;
    public TribulationTier Tier => TribulationTier.EarthCalamity;
    public TribulationDanger Danger => TribulationDanger.Aberrant;
    public int BaseWeight => 1;

    public bool CanAppear(in TribulationSelectionContext context) => true;
    public float GetEnemyCompatibilityMultiplier(in TribulationSelectionContext context) => 1f;

    public Task OnAppliedAsync(TribulationContext context) =>
        EarthCalamitySupport.ApplyAnchorPowerAsync<HeiYanXingZhuiPower>(context);

    public Task OnPlayerHpDamageTakenAsync(TribulationContext context, decimal amount)
    {
        int total = TribulationStateStore.GetCounter(context, PlayerLoss) +
            (int)Math.Ceiling(amount);
        int flame = TribulationStateStore.GetCounter(context, Flame);
        int threshold = Math.Max(
            5,
            8 - TribulationStateStore.GetCounter(context, Scar));
        while (total >= threshold && flame < 4)
        {
            total -= threshold;
            flame++;
        }
        if (flame >= 4)
            total = Math.Min(total, threshold - 1);
        TribulationStateStore.SetCounter(context, PlayerLoss, total);
        TribulationStateStore.SetCounter(context, Flame, flame);
        EarthCalamitySupport.RefreshAnchorPower<HeiYanXingZhuiPower>(context);
        return Task.CompletedTask;
    }

    public Task OnDamageResolvedAsync(
        TribulationContext context,
        TribulationDamageEvent damage)
    {
        if (ReferenceEquals(damage.Target, context.Leader) && damage.HpDamage > 0m)
        {
            TribulationStateStore.AddCounter(
                context,
                LeaderLoss,
                (int)damage.HpDamage,
                0,
                int.MaxValue);
        }
        return Task.CompletedTask;
    }

    public async Task OnPlayerTurnEndAsync(TribulationContext context, int turn)
    {
        int flame = TribulationStateStore.GetCounter(context, Flame);
        int leaderLoss = TribulationStateStore.GetCounter(context, LeaderLoss);
        int counterThreshold = EarthCalamitySupport.PercentCeiling(
            context.Leader,
            0.20m);
        if (leaderLoss >= counterThreshold && flame > 0)
        {
            flame--;
            TribulationStateStore.SetCounter(context, Flame, flame);
        }

        TribulationStateStore.SetCounter(context, LeaderLoss, 0);
        if (flame >= 4)
        {
            TribulationStateStore.SetCounter(context, Flame, 2);
            TribulationStateStore.AddCounter(context, Scar, 1, 0, 3);
            await EarthCalamitySupport.DamagePlayerAsync(
                context,
                EarthCalamitySupport.ScaleFlat(context, 16),
                unblockable: true);
        }
        EarthCalamitySupport.RefreshAnchorPower<HeiYanXingZhuiPower>(context);
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
        int flame = TribulationStateStore.GetCounter(context, Flame);
        if (ReferenceEquals(target, context.Leader))
            return Math.Max(0.10m, 1m - flame * 0.06m);
        if (ReferenceEquals(dealer, context.Leader) && props.IsPoweredAttack())
            return 1m + flame * 0.06m;
        return 1m;
    }

    private static string Key(string local) =>
        TribulationStateStore.Key(TribulationIds.HeiYanXingZhui, local);
}
