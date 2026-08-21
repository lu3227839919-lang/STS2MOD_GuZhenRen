using GuZhenRen.Tribulations.Core;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRen.Tribulations.Contracts;

public interface ITribulationCombatLifecycle
{
    Task OnAppliedAsync(TribulationContext context);
    Task OnCombatVictoryAsync(TribulationContext context);
    Task OnCombatEndAsync(TribulationContext context);
}

public interface ITribulationTurnLifecycle
{
    Task OnPlayerTurnStartAsync(TribulationContext context, int turn);
    Task OnPlayerTurnEndAsync(TribulationContext context, int turn);
}

public interface ITribulationCardObserver
{
    Task OnCardPlayedAsync(TribulationContext context, CardModel card);
    Task OnCardDrawnAsync(TribulationContext context, CardModel card);
    Task OnCardDiscardedAsync(TribulationContext context, CardModel card);
}

public interface ITribulationGuObserver
{
    Task OnGuActivatedAsync(TribulationContext context, CardModel gu);
    Task OnGuRecoveredAsync(TribulationContext context, CardModel gu);
    Task OnGuEnteredStorageAsync(TribulationContext context, CardModel gu);
    Task OnGuEnteredActivePileAsync(TribulationContext context, CardModel gu);
}

public interface ITribulationDamageObserver
{
    Task OnLeaderDamageTakenAsync(TribulationContext context, decimal amount);
    Task OnPlayerHpDamageTakenAsync(TribulationContext context, decimal amount);
    Task OnBlockGainedAsync(TribulationContext context, decimal rawAmount);
}

public interface ITribulationResourceObserver
{
    Task OnYuanQiSpentAsync(TribulationContext context, int amount);
    Task OnNativeEnergySpentAsync(TribulationContext context, int amount);
}

public interface ITribulationPhaseController
{
    string CurrentPhaseId { get; }
    Task EvaluatePhaseTransitionAsync(TribulationContext context);
}

public interface ITribulationCombatModifier
{
    decimal ModifyIncomingDamageToLeader(TribulationContext context, decimal amount);
    decimal ModifyLeaderAttackDamage(TribulationContext context, decimal amount);
    decimal ModifyPlayerBlockGain(TribulationContext context, decimal amount);
    decimal ModifyPlayerHealing(TribulationContext context, decimal amount);
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

public interface IGuSystemCorruptionCard { }
public interface IGuActiveSlotOccupant { }
