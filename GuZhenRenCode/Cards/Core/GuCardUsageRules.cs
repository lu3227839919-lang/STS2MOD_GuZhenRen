using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRen.Cards;

/// <summary>
/// 蛊虫牌的每回合催动次数规则。
///
/// 只统计每次出牌序列的第一段，因此 Replay 等原版重复结算仍视为一次催动。
/// 使用 CardPlayStarted 计数，可阻止同一张牌在上一轮效果尚未结束时再次进入。
/// </summary>
public static class GuCardUsageRules
{
    public static int CountUsesThisTurn(CardModel card)
    {
        ArgumentNullException.ThrowIfNull(card);

        if (!CombatManager.Instance.IsInProgress ||
            card.CombatState is not { } combatState)
        {
            return 0;
        }

        return CombatManager.Instance.History.CardPlaysStarted.Count(entry =>
            ReferenceEquals(entry.CardPlay.Card, card) &&
            entry.CardPlay.PlayIndex == 0 &&
            entry.HappenedThisTurn(combatState)
        );
    }

    public static bool CanUse(CardModel card)
    {
        ArgumentNullException.ThrowIfNull(card);

        if (card is not IGuWormCard guCard)
        {
            return true;
        }

        int limit = Math.Max(0, guCard.MaxUsesPerTurn);
        return CountUsesThisTurn(card) < limit;
    }
}
