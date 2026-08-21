using GuZhenRen.Tribulations.Core;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace GuZhenRen.Tribulations.Contracts;

/// <summary>灾劫应用、胜利与战斗清理生命周期；默认实现均为空操作。</summary>
public interface ITribulationCombatLifecycle
{
    Task OnAppliedAsync(TribulationContext context) => Task.CompletedTask;
    Task OnCombatVictoryAsync(TribulationContext context) => Task.CompletedTask;
    Task OnCombatEndAsync(TribulationContext context) => Task.CompletedTask;
}

/// <summary>玩家和敌方回合边界观察接口。</summary>
public interface ITribulationTurnLifecycle
{
    Task OnPlayerTurnStartAsync(TribulationContext context, int turn) =>
        Task.CompletedTask;
    Task OnPlayerTurnEndAsync(TribulationContext context, int turn) =>
        Task.CompletedTask;
    Task OnEnemyTurnStartAsync(TribulationContext context, int round) =>
        Task.CompletedTask;
    Task OnEnemyTurnEndAsync(TribulationContext context, int round) =>
        Task.CompletedTask;
}

/// <summary>观察打牌、抽牌和弃牌事件。</summary>
public interface ITribulationCardObserver
{
    Task OnCardPlayedAsync(TribulationContext context, CardPlay cardPlay) =>
        Task.CompletedTask;
    Task OnCardDrawnAsync(TribulationContext context, CardModel card) =>
        Task.CompletedTask;
    Task OnCardDiscardedAsync(TribulationContext context, CardModel card) =>
        Task.CompletedTask;
}

/// <summary>观察蛊虫催动、恢复以及专属牌堆迁移。</summary>
public interface ITribulationGuObserver
{
    Task OnGuActivatedAsync(TribulationContext context, CardModel gu) =>
        Task.CompletedTask;
    Task OnGuRecoveredAsync(TribulationContext context, CardModel gu) =>
        Task.CompletedTask;
    Task OnGuEnteredStorageAsync(TribulationContext context, CardModel gu) =>
        Task.CompletedTask;
    Task OnGuEnteredActivePileAsync(TribulationContext context, CardModel gu) =>
        Task.CompletedTask;
}

/// <summary>观察伤害完成、生命损失和格挡命令前后事件。</summary>
public interface ITribulationDamageObserver
{
    Task OnDamageResolvedAsync(
        TribulationContext context,
        TribulationDamageEvent damage) => Task.CompletedTask;
    Task OnLeaderDamageTakenAsync(TribulationContext context, decimal amount) =>
        Task.CompletedTask;
    Task OnPlayerHpDamageTakenAsync(TribulationContext context, decimal amount) =>
        Task.CompletedTask;
    Task OnBeforeBlockGainedAsync(
        TribulationContext context,
        Creature target,
        decimal rawAmount,
        ValueProp props,
        CardModel? cardSource) => Task.CompletedTask;
    Task OnBlockGainedAsync(
        TribulationContext context,
        Creature target,
        decimal finalAmount,
        ValueProp props,
        CardModel? cardSource) => Task.CompletedTask;
}

/// <summary>观察元气/能量消耗，并可修正即将获得的元气。</summary>
public interface ITribulationResourceObserver
{
    Task OnYuanQiSpentAsync(TribulationContext context, int amount) =>
        Task.CompletedTask;
    Task OnNativeEnergySpentAsync(TribulationContext context, int amount) =>
        Task.CompletedTask;
    int ModifyYuanQiGain(TribulationContext context, int amount) => amount;
}

public interface ITribulationPhaseController
{
    string GetCurrentPhaseId(TribulationContext context);
    Task EvaluatePhaseTransitionAsync(TribulationContext context);
}

/// <summary>灾劫战斗数值修正接口；默认返回值均保持原版行为。</summary>
public interface ITribulationCombatModifier
{
    decimal ModifyDamageAdditive(
        TribulationContext context,
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay) => 0m;

    decimal ModifyDamageMultiplicative(
        TribulationContext context,
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay) => 1m;

    decimal ModifyDamageCap(
        TribulationContext context,
        Creature? target,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay) => decimal.MaxValue;

    decimal ModifyPlayerBlockGain(
        TribulationContext context,
        Creature target,
        decimal amount,
        ValueProp props,
        CardModel? cardSource,
        CardPlay? cardPlay) => amount;

    decimal ModifyPlayerHealing(TribulationContext context, decimal amount) => amount;

    bool TryModifyPowerAmountReceived(
        TribulationContext context,
        PowerModel canonicalPower,
        Creature target,
        decimal amount,
        Creature? applier,
        out decimal modifiedAmount)
    {
        modifiedAmount = amount;
        return false;
    }
}

public interface ITribulationScalingProvider
{
    TribulationScaling ResolveScaling(in TribulationContext context);
}

public readonly record struct TribulationScaling(
    decimal NumericMultiplier,
    decimal HpBonus,
    int ThresholdAdjustment
);

/// <summary>伤害命令完成后的只读事件快照，保留来源牌与打牌上下文。</summary>
public sealed record TribulationDamageEvent(
    Creature Target,
    Creature? Dealer,
    DamageResult Result,
    ValueProp Props,
    CardModel? CardSource,
    CardPlay? CardPlay)
{
    public decimal HpDamage => Math.Max(
        0,
        Result.UnblockedDamage - Result.OverkillDamage);
    public decimal Blocked => Result.BlockedDamage;
    public decimal TotalDamage => Result.TotalDamage;
    public bool IsAttack => Props.IsPoweredAttack();
}

public interface ITribulationGeneratedObject { }
public interface IGuSystemCorruptionCard { }
public interface IGuActiveSlotOccupant
{
    int OccupiedActiveSlots { get; }
}
