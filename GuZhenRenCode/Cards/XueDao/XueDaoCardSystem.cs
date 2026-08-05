using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRen.Cards.XueDao;

internal static class XueDaoCardSystem
{
    internal static IReadOnlyList<YiHai> GetRemains(Player owner) =>
        PileType.Hand
            .GetPile(owner)
            .Cards
            .OfType<YiHai>()
            .ToArray();

    internal static int CountRemains(Player owner) =>
        GetRemains(owner).Count;

    internal static async Task<int> ConsumeOldestRemains(
        PlayerChoiceContext choiceContext,
        Player owner,
        int maximum
    )
    {
        if (maximum <= 0)
        {
            return 0;
        }

        YiHai[] remains = GetRemains(owner)
            .Take(maximum)
            .ToArray();

        foreach (YiHai card in remains)
        {
            await CardCmd.Exhaust(choiceContext, card);
        }

        return remains.Length;
    }

    internal static async Task AddRemains(
        Player owner,
        int amount
    )
    {
        for (int index = 0; index < amount; index++)
        {
            YiHai card = GuGeneratedCardFactory.Create<YiHai>(
                owner,
                guRank: 1
            );
            await GuGeneratedCardFactory.AddToHandOrDiscard(card, owner);
        }
    }
}
