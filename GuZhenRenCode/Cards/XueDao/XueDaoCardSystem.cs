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

        YiHai[] available = GetRemains(owner)
            .ToArray();

        int consumeCount = Math.Min(maximum, available.Length);
        if (consumeCount <= 0)
        {
            return 0;
        }

        LocString prompt = new(
            "cards",
            "GU_ZHEN_REN_CARD_YI_HAI.consumeSelectionPrompt"
        );
        CardSelectorPrefs prefs = new(prompt, consumeCount)
        {
            Cancelable = false,
            RequireManualConfirmation = available.Length > consumeCount,
            PretendCardsCanBePlayed = true,
        };

        // 只传入遗骸候选，其他手牌不会出现在界面。候选顺序沿用同步
        // 手牌顺序，FromSimpleGrid 以索引同步，适配多人。
        YiHai[] remains = (
                await CardSelectCmd.FromSimpleGrid(
                    choiceContext,
                    available,
                    owner,
                    prefs
                )
            )
            .OfType<YiHai>()
            .Distinct()
            .Where(card =>
                ReferenceEquals(card.Owner, owner) &&
                card.Pile?.Type == PileType.Hand &&
                available.Contains(card)
            )
            .Take(consumeCount)
            .ToArray();

        if (remains.Length != consumeCount)
        {
            Entry.Logger.Warn(
                $"遗骸选择失效：期望 {consumeCount} 张，实际 " +
                $"{remains.Length} 张。本次不消耗遗骸。"
            );
            return 0;
        }

        foreach (YiHai card in remains)
        {
            await CardExhaustCompat.ExhaustAsync(choiceContext, card);
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
