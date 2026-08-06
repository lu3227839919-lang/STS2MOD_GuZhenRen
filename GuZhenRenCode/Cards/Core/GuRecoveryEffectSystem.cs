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
        // 0.110.0 中 AfterCardPlayed 可能早于出牌结果牌堆迁移完成。
        // 此时卡牌会短暂处于无牌堆状态，按 card.Pile 判断将漏掉全部
        // “进入恢复堆”效果。BeforeCardPlayed 已经登记催动次数，因此
        // 以“催动次数已耗尽”作为稳定判据；仍有剩余次数的蛊虫不会误触发。
        return card is IGuRecoveryEffectSource source &&
               card is IGuWormCard &&
               !GuCardUsageRules.CanUse(card)
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
