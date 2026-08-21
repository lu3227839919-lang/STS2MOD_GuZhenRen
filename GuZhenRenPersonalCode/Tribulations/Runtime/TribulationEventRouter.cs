// ============================================================================
// 中文维护说明
// 文件职责：负责把已保存的灾劫选择恢复为战斗效果，并路由战斗事件。
// 主要类型：TribulationEventRouter。
// 实现要点：灾劫能力按所实现的细粒度接口由事件路由选择性分派。
// 维护约定：灾劫选择先持久化再应用；新增钩子时同步更新事件路由和幂等标记。
// ============================================================================
using GuZhenRen.Tribulations.Contracts;
using GuZhenRen.Tribulations.Core;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace GuZhenRen.Tribulations.Runtime;

/// <summary>
/// 游戏原生战斗钩子与当前灾劫之间的唯一事件桥。每个入口先恢复活动上下文，
/// 再按定义实际实现的细粒度接口转发；未实现的事件保持原版默认值。
/// </summary>
public sealed class TribulationEventRouter(
    TribulationRegistry registry,
    TribulationRuntime runtime)
{
    public async Task OnPlayerTurnStartAsync(Player player, int turn)
    {
        if (TryGet<ITribulationTurnLifecycle>(player, out var x, out var c))
            await x.OnPlayerTurnStartAsync(c, turn);
    }

    public async Task OnPlayerTurnEndAsync(Player player, int turn)
    {
        if (TryGet<ITribulationTurnLifecycle>(player, out var x, out var c))
            await x.OnPlayerTurnEndAsync(c, turn);
    }

    public async Task OnEnemyTurnStartAsync(Player player, int round)
    {
        if (TryGet<ITribulationTurnLifecycle>(player, out var x, out var c))
            await x.OnEnemyTurnStartAsync(c, round);
    }

    public async Task OnEnemyTurnEndAsync(Player player, int round)
    {
        if (TryGet<ITribulationTurnLifecycle>(player, out var x, out var c))
            await x.OnEnemyTurnEndAsync(c, round);
    }

    public async Task OnCardPlayedAsync(Player player, CardPlay cardPlay)
    {
        if (TryGet<ITribulationCardObserver>(player, out var x, out var c))
            await x.OnCardPlayedAsync(c, cardPlay);
    }

    public async Task OnCardDrawnAsync(Player player, CardModel card)
    {
        if (TryGet<ITribulationCardObserver>(player, out var x, out var c))
            await x.OnCardDrawnAsync(c, card);
    }

    public async Task OnCardDiscardedAsync(Player player, CardModel card)
    {
        if (TryGet<ITribulationCardObserver>(player, out var x, out var c))
            await x.OnCardDiscardedAsync(c, card);
    }

    public async Task OnGuActivatedAsync(Player player, CardModel card)
    {
        if (TryGet<ITribulationGuObserver>(player, out var x, out var c))
            await x.OnGuActivatedAsync(c, card);
    }

    public async Task OnYuanQiSpentAsync(Player player, int amount)
    {
        if (amount > 0 && TryGet<ITribulationResourceObserver>(player, out var x, out var c))
            await x.OnYuanQiSpentAsync(c, amount);
    }

    public async Task OnNativeEnergySpentAsync(Player player, int amount)
    {
        if (amount > 0 && TryGet<ITribulationResourceObserver>(player, out var x, out var c))
            await x.OnNativeEnergySpentAsync(c, amount);
    }

    public int ModifyYuanQiGain(Player player, int amount) =>
        TryGet<ITribulationResourceObserver>(player, out var x, out var c)
            ? x.ModifyYuanQiGain(c, amount)
            : amount;

    public async Task OnDamageResolvedAsync(
        Player player,
        TribulationDamageEvent damage)
    {
        if (TryGet<ITribulationDamageObserver>(player, out var x, out var c))
            await x.OnDamageResolvedAsync(c, damage);
    }

    public async Task OnPlayerHpLostAsync(Player player, decimal amount)
    {
        if (amount > 0m && TryGet<ITribulationDamageObserver>(player, out var x, out var c))
            await x.OnPlayerHpDamageTakenAsync(c, amount);
    }

    public async Task OnBeforeBlockGainedAsync(
        Player player,
        Creature target,
        decimal amount,
        ValueProp props,
        CardModel? cardSource)
    {
        if (TryGet<ITribulationDamageObserver>(player, out var x, out var c))
            await x.OnBeforeBlockGainedAsync(c, target, amount, props, cardSource);
    }

    public async Task OnBlockGainedAsync(
        Player player,
        Creature target,
        decimal amount,
        ValueProp props,
        CardModel? cardSource)
    {
        if (TryGet<ITribulationDamageObserver>(player, out var x, out var c))
            await x.OnBlockGainedAsync(c, target, amount, props, cardSource);
    }

    public decimal ModifyDamageAdditive(
        Player player,
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay) =>
        TryGet<ITribulationCombatModifier>(player, out var x, out var c)
            ? x.ModifyDamageAdditive(c, target, amount, props, dealer, cardSource, cardPlay)
            : 0m;

    public decimal ModifyDamageMultiplicative(
        Player player,
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay) =>
        TryGet<ITribulationCombatModifier>(player, out var x, out var c)
            ? x.ModifyDamageMultiplicative(c, target, amount, props, dealer, cardSource, cardPlay)
            : 1m;

    public decimal ModifyDamageCap(
        Player player,
        Creature? target,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay) =>
        TryGet<ITribulationCombatModifier>(player, out var x, out var c)
            ? x.ModifyDamageCap(c, target, props, dealer, cardSource, cardPlay)
            : decimal.MaxValue;

    public decimal ModifyPlayerBlockGain(
        Player player,
        Creature target,
        decimal amount,
        ValueProp props,
        CardModel? cardSource,
        CardPlay? cardPlay) =>
        TryGet<ITribulationCombatModifier>(player, out var x, out var c)
            ? x.ModifyPlayerBlockGain(c, target, amount, props, cardSource, cardPlay)
            : amount;

    public bool TryModifyPowerAmountReceived(
        Player player,
        PowerModel canonicalPower,
        Creature target,
        decimal amount,
        Creature? applier,
        out decimal modifiedAmount)
    {
        if (TryGet<ITribulationCombatModifier>(player, out var x, out var c))
            return x.TryModifyPowerAmountReceived(
                c, canonicalPower, target, amount, applier, out modifiedAmount);
        modifiedAmount = amount;
        return false;
    }

    /// <summary>统一执行楼层、首领存活和接口类型检查。</summary>
    private bool TryGet<T>(
        Player player,
        out T definition,
        out TribulationContext context)
        where T : class
    {
        TribulationContext? current = runtime.TryBuildActiveContext(player);
        object? selected = current == null
            ? null
            : registry.GetRequired(current.Selection.TribulationId);
        if (current != null && selected is T typed)
        {
            definition = typed;
            context = current;
            return true;
        }

        definition = null!;
        context = null!;
        return false;
    }
}
