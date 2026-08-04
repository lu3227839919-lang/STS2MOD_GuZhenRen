using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRen.Cards;

internal static class GuGeneratedCardFactory
{
    internal static T Create<T>(
        Player owner,
        int guRank,
        bool upgraded = false
    ) where T : AbstractGuZhenRenCard
    {
        if (owner.Creature.CombatState is not { } combatState)
        {
            throw new InvalidOperationException(
                "Cannot create a generated Gu card outside combat."
            );
        }

        T card = (T)combatState.CreateCard(
            ModelDb.Card<T>(),
            owner
        );
        card.InitializeGuRankFromSource(guRank);

        if (upgraded)
        {
            CardCmd.Upgrade(card);
        }

        return card;
    }

    internal static async Task AddToHandOrDiscard(
        CardModel card,
        Player owner
    )
    {
        bool added = await GuCardPileSystem.AddGeneratedCardToHand(
            card,
            owner
        );

        if (!added)
        {
            await GuCardPileSystem.AddGeneratedCardToDiscard(
                card,
                owner
            );
        }
    }
}
