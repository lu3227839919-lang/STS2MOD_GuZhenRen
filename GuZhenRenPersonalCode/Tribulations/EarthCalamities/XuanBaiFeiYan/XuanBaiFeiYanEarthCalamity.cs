// ============================================================================
// 中文维护说明
// 文件职责：实现地灾注入战斗循环的状态牌；对应本地化名称“玄白飞盐”。
// 主要类型：XuanBaiFeiYanEarthCalamity。
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

namespace GuZhenRen.Tribulations.EarthCalamities.XuanBaiFeiYan;

public sealed class XuanBaiFeiYanEarthCalamity :
    ITribulationDefinition,
    ITribulationCombatLifecycle,
    ITribulationTurnLifecycle,
    ITribulationDamageObserver,
    ITribulationCardObserver,
    ITribulationResourceObserver,
    ITribulationCombatModifier
{
    private static readonly string Shell = Key("salt_shell");
    private static readonly string Salt = Key("salt_accumulation");
    private static readonly string BrokeThisTurn = Key("broke_shell_this_turn");
    private static readonly string PendingBreak = Key("pending_shell_break");
    private static readonly string YuanQiPenalty = Key("yuan_qi_gain_penalty");
    private static readonly string Initialized = Key("initialized");

    public string Id => TribulationIds.XuanBaiFeiYan;
    public TribulationTier Tier => TribulationTier.EarthCalamity;
    public TribulationDanger Danger => TribulationDanger.Common;
    public int BaseWeight => 1;

    public bool CanAppear(in TribulationSelectionContext context) => true;
    public float GetEnemyCompatibilityMultiplier(
        in TribulationSelectionContext context) => 1f;

    public async Task OnAppliedAsync(TribulationContext context)
    {
        if (!TribulationStateStore.GetFlag(context, Initialized))
        {
            TribulationStateStore.SetCounter(context, Shell, 3);
            TribulationStateStore.SetFlag(context, Initialized, true);
        }
        await EarthCalamitySupport.ApplyAnchorPowerAsync<XuanBaiFeiYanPower>(context);
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
        if (ReferenceEquals(target, context.Leader) &&
            TribulationStateStore.GetCounter(context, Shell) > 0 &&
            amount > EarthCalamitySupport.ScaleFlat(context, 14) &&
            (cardPlay != null || cardSource == null))
        {
            TribulationStateStore.SetFlag(context, PendingBreak, true);
        }
        return 1m;
    }

    public decimal ModifyDamageCap(
        TribulationContext context,
        Creature? target,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay) =>
        ReferenceEquals(target, context.Leader) &&
        TribulationStateStore.GetCounter(context, Shell) > 0
            ? EarthCalamitySupport.ScaleFlat(context, 14)
            : decimal.MaxValue;

    public Task OnDamageResolvedAsync(
        TribulationContext context,
        TribulationDamageEvent damage)
    {
        if (!ReferenceEquals(damage.Target, context.Leader))
            return Task.CompletedTask;

        bool shouldBreak = TribulationStateStore.GetFlag(context, PendingBreak);
        TribulationStateStore.SetFlag(context, PendingBreak, false);
        if (!shouldBreak || damage.TotalDamage <= 0)
            return Task.CompletedTask;

        TribulationStateStore.AddCounter(context, Shell, -1, 0, 5);
        TribulationStateStore.SetFlag(context, BrokeThisTurn, true);
        EarthCalamitySupport.RefreshAnchorPower<XuanBaiFeiYanPower>(context);
        return Task.CompletedTask;
    }

    public async Task OnEnemyTurnEndAsync(TribulationContext context, int round)
    {
        if (TribulationStateStore.GetFlag(context, BrokeThisTurn))
        {
            TribulationStateStore.SetFlag(context, BrokeThisTurn, false);
            return;
        }

        int salt = TribulationStateStore.AddCounter(context, Salt, 1, 0, 2);
        if (salt < 2)
            return;

        TribulationStateStore.SetCounter(context, Salt, 0);
        int shell = TribulationStateStore.GetCounter(context, Shell);
        if (shell < 5)
            TribulationStateStore.SetCounter(context, Shell, shell + 1);
        else
            await EarthCalamitySupport.AddStatusToDiscardAsync<YanShiStatusCard>(context);
        EarthCalamitySupport.RefreshAnchorPower<XuanBaiFeiYanPower>(context);
    }

    public Task OnCardDrawnAsync(TribulationContext context, CardModel card)
    {
        if (card is YanShiStatusCard)
            TribulationStateStore.AddCounter(context, YuanQiPenalty, 1, 0, 99);
        return Task.CompletedTask;
    }

    public int ModifyYuanQiGain(TribulationContext context, int amount)
    {
        int penalty = TribulationStateStore.GetCounter(context, YuanQiPenalty);
        if (penalty <= 0)
            return amount;
        TribulationStateStore.SetCounter(context, YuanQiPenalty, 0);
        return Math.Max(0, amount - penalty);
    }

    public Task OnPlayerTurnEndAsync(TribulationContext context, int turn)
    {
        TribulationStateStore.SetCounter(context, YuanQiPenalty, 0);
        return Task.CompletedTask;
    }

    private static string Key(string local) =>
        TribulationStateStore.Key(TribulationIds.XuanBaiFeiYan, local);
}
