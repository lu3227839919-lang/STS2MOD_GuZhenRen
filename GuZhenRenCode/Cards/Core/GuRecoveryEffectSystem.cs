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
        return card is IGuRecoveryEffectSource source &&
               card.Pile?.Type == GuCardPileSystem.RecoveryPileType
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
