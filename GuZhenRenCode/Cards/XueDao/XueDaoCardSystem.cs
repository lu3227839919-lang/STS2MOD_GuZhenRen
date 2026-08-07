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
    /// <summary>
    /// 遗骸在永久牌堆中最多保存的张数。超出后击杀不再生成新遗骸。
    /// </summary>
    internal const int MaxPersistentRemains = 4;

    internal static IReadOnlyList<YiHai> GetRemains(Player owner) =>
        PileType.Hand
            .GetPile(owner)
            .Cards
            .OfType<YiHai>()
            .ToArray();

    internal static int CountRemains(Player owner) =>
        GetRemains(owner).Count;

    /// <summary>
    /// 统计最终会保留到永久牌堆的遗骸数量：永久牌堆加上战斗中的
    /// 抽牌堆/弃牌堆/手牌。已消耗（进入 Exhaust 堆）的遗骸不计入，
    /// 因此主动消耗后名额会被释放。
    /// </summary>
    internal static int CountPersistentRemains(Player owner) =>
        new[]
            {
                owner.Deck,
                PileType.Draw.GetPile(owner),
                PileType.Discard.GetPile(owner),
                PileType.Hand.GetPile(owner),
            }
            .Sum(pile =>
                pile.Cards.Count(static card => card is YiHai)
            );

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
        // 永久牌堆中遗骸最多 4 张：已达上限时不再生成新遗骸。
        int persistent = CountPersistentRemains(owner);
        int slots = Math.Max(
            0,
            MaxPersistentRemains - persistent
        );
        int toAdd = Math.Min(amount, slots);

        Entry.Logger.Info(
            $"[遗骸生成] {owner.NetId} 请求 {amount} 张，" +
            $"现有 {persistent}，本次生成 {toAdd} 张。"
        );

        for (int index = 0; index < toAdd; index++)
        {
            YiHai card = GuGeneratedCardFactory.Create<YiHai>(
                owner,
                guRank: 1
            );

            bool addedToHand =
                await GuCardPileSystem.AddGeneratedCardToHand(
                    card,
                    owner
                );

            Entry.Logger.Info(
                $"[遗骸生成] 遗骸 {card.Id} 入手牌:{addedToHand}，" +
                $"当前牌堆:{card.Pile?.Type.ToString() ?? "null"}。"
            );

            if (!addedToHand)
            {
                // 手牌/弃牌堆拒绝新卡通常发生在最后一击（CombatManager
                // IsEnding，战斗即将结束）时。这种击杀产生的遗骸没有
                // 机会再使用，直接加入永久牌组保留。
                // 战斗卡只登记在 CombatState，先登记到 RunState 才能入牌组。
                owner.RunState.AddCard(card, owner);

                CardPileAddResult deckResult =
                    await CardPileCmd.Add(
                        card,
                        PileType.Deck,
                        CardPilePosition.Bottom,
                        clonedBy: null,
                        skipVisuals: true
                    );

                Entry.Logger.Info(
                    $"[遗骸生成] 遗骸 {card.Id} 直接入永久牌组:" +
                    $"{deckResult.success}。"
                );
            }
        }
    }
}
