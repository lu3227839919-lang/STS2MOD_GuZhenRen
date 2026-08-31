using GuZhenRen.Multiplayer;

using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRen.Cards.XueDao;

internal static class XueDaoCardSystem
{
    internal const int MaxPersistentRemains = 4;

    internal static IReadOnlyList<YiHai> GetRemains(Player owner) =>
        PileType.Hand
            .GetPile(owner)
            .Cards
            .OfType<YiHai>()
            .OrderBy(card => card.Id.ToString(), StringComparer.Ordinal)
            .ThenBy(GuZhenRenDeterminism.GetCardNetworkId)
            .ToArray();

    internal static int CountPersistentRemains(Player owner)
    {
        int deckOriginals = owner.Deck.Cards.Count(static card => card is YiHai);
        int newCombatRemains = new[]
            {
                PileType.Draw.GetPile(owner),
                PileType.Discard.GetPile(owner),
                PileType.Hand.GetPile(owner),
            }
            .SelectMany(static pile => pile.Cards)
            .OfType<YiHai>()
            .Where(static card => card.DeckVersion == null)
            .Distinct()
            .Count();

        return deckOriginals + newCombatRemains;
    }

    internal static async Task ConsumeRemainCard(
        PlayerChoiceContext choiceContext,
        YiHai card
    )
    {
        CardModel? deckOriginal = card.DeckVersion;
        await CardExhaustCompat.ExhaustAsync(choiceContext, card);

        if (deckOriginal?.Pile?.Type == PileType.Deck)
        {
            await CardPileCmd.RemoveFromDeck(deckOriginal, showPreview: false);
        }
    }

    internal static async Task<int> ConsumeSelectedRemains(
        PlayerChoiceContext choiceContext,
        Player owner,
        int maximum
    )
    {
        int consumed = 0;
        while (consumed < maximum)
        {
            YiHai[] available = GetRemains(owner).ToArray();
            if (available.Length == 0)
            {
                break;
            }

            LocString prompt = new(
                "cards",
                "GU_ZHEN_REN_PERSONAL_CARD_YI_HAI.consumeSelectionPrompt"
            );
            CardSelectorPrefs prefs = new(prompt, 1)
            {
                Cancelable = true,
                RequireManualConfirmation = true,
                PretendCardsCanBePlayed = true,
            };

            YiHai? selected = (
                    await CardSelectCmd.FromSimpleGrid(
                        choiceContext,
                        available,
                        owner,
                        prefs
                    )
                )
                .OfType<YiHai>()
                .FirstOrDefault();

            if (selected == null)
            {
                break;
            }

            if (!ReferenceEquals(selected.Owner, owner) ||
                selected.Pile?.Type != PileType.Hand ||
                !available.Contains(selected))
            {
                Entry.Logger.Warn("炼骸选择在同步后失效，本次停止炼骸。");
                break;
            }

            await ConsumeRemainCard(choiceContext, selected);
            consumed++;
        }

        return consumed;
    }

    internal static async Task AddRemains(Player owner, int amount)
    {
        int toAdd = Math.Min(
            Math.Max(0, amount),
            Math.Max(
                0,
                MaxPersistentRemains - CountPersistentRemains(owner)
            )
        );

        for (int index = 0; index < toAdd; index++)
        {
            YiHai card = GuGeneratedCardFactory.Create<YiHai>(owner, guRank: 1);
            bool addedToHand = await GuCardPileSystem.AddGeneratedCardToHand(
                card,
                owner
            );
            if (addedToHand)
            {
                continue;
            }

            card.RemoveFromCurrentPile();
            card.Owner = null!;
            owner.RunState.AddCard(card, owner);
            await CardPileCmd.Add(
                card,
                PileType.Deck,
                CardPilePosition.Bottom,
                clonedBy: null,
                skipVisuals: true
            );
        }
    }
}
