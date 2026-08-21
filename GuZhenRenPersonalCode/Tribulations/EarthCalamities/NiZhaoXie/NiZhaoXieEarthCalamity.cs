// ============================================================================
// 中文维护说明
// 文件职责：实现地灾注入战斗循环的状态牌；对应本地化名称“荒兽泥沼蟹”。
// 主要类型：NiZhaoXieEarthCalamity。
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

namespace GuZhenRen.Tribulations.EarthCalamities.NiZhaoXie;

public sealed class NiZhaoXieEarthCalamity :
    ITribulationDefinition,
    ITribulationCombatLifecycle,
    ITribulationTurnLifecycle,
    ITribulationDamageObserver,
    ITribulationCardObserver,
    ITribulationCombatModifier
{
    private static readonly string Silt = Key("silt");
    private static readonly string ArmorActive = Key("armor_active");
    private static readonly string ArmorBroken = Key("armor_broken");
    private static readonly string ArmorBaseBlock = Key("armor_base_block");
    private static readonly string ArmorIssued = Key("armor_issued");
    private static readonly string Cracked = Key("cracked");
    private static readonly string MudPenalty = Key("mud_barrier_pending");

    public string Id => TribulationIds.NiZhaoXie;
    public TribulationTier Tier => TribulationTier.EarthCalamity;
    public TribulationDanger Danger => TribulationDanger.Common;
    public int BaseWeight => 1;

    public bool CanAppear(in TribulationSelectionContext context) => true;
    public float GetEnemyCompatibilityMultiplier(
        in TribulationSelectionContext context) => 1f;

    public Task OnAppliedAsync(TribulationContext context) =>
        EarthCalamitySupport.ApplyAnchorPowerAsync<NiZhaoXiePower>(context);

    public async Task OnPlayerTurnStartAsync(TribulationContext context, int turn)
    {
        TribulationStateStore.SetFlag(context, Cracked, false);
        if (turn % 2 == 0)
            return;

        int armor = Math.Max(
            EarthCalamitySupport.ScaleFlat(context, 20),
            EarthCalamitySupport.PercentCeiling(context.Leader, 0.20m));
        armor += TribulationStateStore.GetCounter(context, Silt) *
            EarthCalamitySupport.ScaleFlat(context, 5);
        TribulationStateStore.SetCounter(context, ArmorBaseBlock, context.Leader.Block);
        TribulationStateStore.SetCounter(context, ArmorIssued, armor);
        TribulationStateStore.SetFlag(context, ArmorBroken, false);
        TribulationStateStore.SetFlag(context, ArmorActive, true);
        await EarthCalamitySupport.GainBlockAsync(context.Leader, armor);
    }

    public Task OnDamageResolvedAsync(
        TribulationContext context,
        TribulationDamageEvent damage)
    {
        if (!ReferenceEquals(damage.Target, context.Leader) ||
            !TribulationStateStore.GetFlag(context, ArmorActive) ||
            TribulationStateStore.GetFlag(context, ArmorBroken))
            return Task.CompletedTask;

        int baseline = TribulationStateStore.GetCounter(context, ArmorBaseBlock);
        if (context.Leader.Block > baseline)
            return Task.CompletedTask;

        TribulationStateStore.SetFlag(context, ArmorBroken, true);
        TribulationStateStore.SetFlag(context, Cracked, true);
        TribulationStateStore.AddCounter(context, Silt, -1, 0, 3);
        EarthCalamitySupport.RefreshAnchorPower<NiZhaoXiePower>(context);
        return Task.CompletedTask;
    }

    public async Task OnPlayerTurnEndAsync(TribulationContext context, int turn)
    {
        if (!TribulationStateStore.GetFlag(context, ArmorActive))
            return;

        TribulationStateStore.SetFlag(context, ArmorActive, false);
        TribulationStateStore.SetFlag(context, Cracked, false);
        if (TribulationStateStore.GetFlag(context, ArmorBroken))
            return;

        int baseline = TribulationStateStore.GetCounter(context, ArmorBaseBlock);
        int issued = TribulationStateStore.GetCounter(context, ArmorIssued);
        int remaining = Math.Clamp(context.Leader.Block - baseline, 0, issued);
        int healing = Math.Min(
            (int)Math.Ceiling(remaining * 0.50m),
            EarthCalamitySupport.PercentCeiling(context.Leader, 0.10m));
        if (healing > 0)
            await EarthCalamitySupport.HealAsync(context.Leader, healing);

        int silt = TribulationStateStore.GetCounter(context, Silt);
        if (silt >= 3)
            await EarthCalamitySupport.AddStatusToDiscardAsync<NiZhangStatusCard>(context);
        else
            TribulationStateStore.SetCounter(context, Silt, silt + 1);
        EarthCalamitySupport.RefreshAnchorPower<NiZhaoXiePower>(context);
    }

    public decimal ModifyDamageMultiplicative(
        TribulationContext context,
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay) =>
        ReferenceEquals(target, context.Leader) &&
        TribulationStateStore.GetFlag(context, Cracked)
            ? 1.25m
            : 1m;

    public Task OnCardDrawnAsync(TribulationContext context, CardModel card)
    {
        if (card is NiZhangStatusCard)
            TribulationStateStore.AddCounter(context, MudPenalty, 1, 0, 99);
        return Task.CompletedTask;
    }

    public decimal ModifyPlayerBlockGain(
        TribulationContext context,
        Creature target,
        decimal amount,
        ValueProp props,
        CardModel? cardSource,
        CardPlay? cardPlay) =>
        ReferenceEquals(target, context.Player.Creature) &&
        TribulationStateStore.GetCounter(context, MudPenalty) > 0
            ? amount * 0.50m
            : amount;

    public Task OnBlockGainedAsync(
        TribulationContext context,
        Creature target,
        decimal finalAmount,
        ValueProp props,
        CardModel? cardSource)
    {
        if (ReferenceEquals(target, context.Player.Creature) && finalAmount > 0m)
            TribulationStateStore.AddCounter(context, MudPenalty, -1, 0, 99);
        return Task.CompletedTask;
    }

    private static string Key(string local) =>
        TribulationStateStore.Key(TribulationIds.NiZhaoXie, local);
}
