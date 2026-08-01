using GuZhenRen.Aperture;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Utils;

namespace GuZhenRen.Cards.ImmortalEssence;

/// <summary>
/// 把手牌中的仙元牌作为催动仙蛊的可分次消耗货币。
///
/// 最小单位为一次六转仙蛊催动：
/// 青提、红枣、白荔、黄杏分别值 2、4、8、16 个单位；
/// 六至九转仙蛊每次分别消耗 1、2、4、8 个单位。
/// </summary>
public static class ImmortalEssenceSystem
{
    private static readonly SavedAttachedState<CardModel, int>
        RemainingUnitsState = new(
            "gu_zhen_ren.immortal_essence_remaining_units",
            () => -1
        );

    public static int GetActivationCost(int guRank)
    {
        if (guRank < ApertureProgression.ImmortalRank)
        {
            return 0;
        }

        int exponent = Math.Clamp(
            guRank - ApertureProgression.ImmortalRank,
            0,
            3
        );
        return 1 << exponent;
    }

    public static int GetRemainingUnits(AbstractXianYuanCard card)
    {
        ArgumentNullException.ThrowIfNull(card);

        int saved = RemainingUnitsState[card];
        return saved < 0
            ? card.ActivationUnits
            : Math.Clamp(saved, 0, card.ActivationUnits);
    }

    public static int GetAvailableUnits(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);

        return player.PlayerCombatState?.Hand.Cards
            .OfType<AbstractXianYuanCard>()
            .Sum(GetRemainingUnits) ?? 0;
    }

    public static bool CanPayForActivation(CardModel card)
    {
        ArgumentNullException.ThrowIfNull(card);

        if (card is not IGuWormCard guCard ||
            guCard.GuRank < ApertureProgression.ImmortalRank)
        {
            return true;
        }

        Player? owner = card.Owner;
        return owner?.PlayerCombatState != null &&
               GetAvailableUnits(owner) >=
               GetActivationCost(guCard.GuRank);
    }

    /// <summary>
    /// 在仙蛊出牌序列的第一段结算仙元。耗尽的仙元牌进入消耗牌堆；
    /// 尚有余额的牌继续保留在手中，供后续回合催动。
    /// </summary>
    public static async Task<bool> SpendForActivation(
        CardPlay cardPlay
    )
    {
        ArgumentNullException.ThrowIfNull(cardPlay);

        if (cardPlay.PlayIndex != 0 ||
            cardPlay.Card is not IGuWormCard guCard ||
            guCard.GuRank < ApertureProgression.ImmortalRank)
        {
            return true;
        }

        Player player = cardPlay.Player;
        int remainingCost = GetActivationCost(guCard.GuRank);

        AbstractXianYuanCard[] availableCards =
            player.PlayerCombatState?.Hand.Cards
                .OfType<AbstractXianYuanCard>()
                .OrderBy(GetRemainingUnits)
                .ThenBy(card => card.Id.Entry, StringComparer.Ordinal)
                .ToArray() ?? [];

        if (availableCards.Sum(GetRemainingUnits) < remainingCost)
        {
            return false;
        }

        List<AbstractXianYuanCard> depletedCards = [];

        foreach (AbstractXianYuanCard essence in availableCards)
        {
            if (remainingCost <= 0)
            {
                break;
            }

            int current = GetRemainingUnits(essence);
            int spent = Math.Min(current, remainingCost);
            int newAmount = current - spent;

            RemainingUnitsState[essence] = newAmount;
            remainingCost -= spent;

            if (newAmount == 0)
            {
                depletedCards.Add(essence);
            }
        }

        foreach (AbstractXianYuanCard depleted in depletedCards)
        {
            await CardCmd.Exhaust(
                new BlockingPlayerChoiceContext(),
                depleted
            );
        }

        return remainingCost == 0;
    }
}
