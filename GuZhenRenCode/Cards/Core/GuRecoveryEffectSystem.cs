using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRen.Cards;

/// <summary>
/// 恢复牌堆阶段效果的统一调度入口。
/// </summary>
internal static class GuRecoveryEffectSystem
{
    internal static Task HandleEnteredRecoveryAsync(CardModel card)
    {
        // AfterCardPlayed can run before the native card-play pipeline has
        // physically inserted the card into its result pile.  Checking
        // card.Pile here therefore drops the event while the depleted Gu is
        // still in the play area.  The activation and recovery schedule are
        // already authoritative at this point, so use them as the gate and
        // let the per-card handled state prevent duplicate processing.
        return card is IGuRecoveryEffectSource source &&
               !GuCardUsageRules.CanUse(card) &&
               GuCardUsageRules.HasRecoverySchedule(card)
            ? source.OnEnteredRecoveryAsync()
            : Task.CompletedTask;
    }

    internal static Task HandleRecoveryTurnStartAsync(
        CardModel card,
        int turnNumber
    )
    {
        return card is IGuRecoveryEffectSource source &&
               card.Pile?.Type == GuCardPileSystem.RecoveryPileType
            ? source.OnRecoveryTurnStartAsync(turnNumber)
            : Task.CompletedTask;
    }

    internal static Task HandleRecoveredAsync(CardModel card)
    {
        return card is IGuRecoveryEffectSource source
            ? source.OnRecoveredAsync()
            : Task.CompletedTask;
    }
}
