using GuZhenRen.Cards;

using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRen.Multiplayer;

/// <summary>
/// 多人端之间必须使用相同的实体顺序后再消费同步随机数。
/// IEnumerable 的枚举顺序不是网络协议的一部分，直接随机索引可能让
/// 各端选中不同目标。
/// </summary>
internal static class GuZhenRenDeterminism
{
    private readonly record struct GuCardIdentity(
        string CardId,
        int UpgradeLevel,
        string EnchantmentId
    );

    /// <summary>
    /// 战斗卡网络编号由原生多人层同步，可作为同 ID、同转数卡牌的
    /// 最终稳定排序键。未登记的预览/牌组模型排在已登记战斗卡之后。
    /// </summary>
    internal static uint GetCardNetworkId(CardModel card)
    {
        ArgumentNullException.ThrowIfNull(card);

        return NetCombatCardDb.Instance.TryGetCardId(
            card,
            out uint netId
        )
            ? netId
            : uint.MaxValue;
    }

    /// <summary>
    /// 永久牌组以槽位编号作为原生多人协议中的卡牌身份。战斗克隆通过
    /// DeckVersion 回溯到原件；不属于永久牌组的卡排在最后。
    /// </summary>
    internal static int GetDeckCardIndex(CardModel card)
    {
        ArgumentNullException.ThrowIfNull(card);

        CardModel deckCard = card.DeckVersion ?? card;
        CardPile? pile = deckCard.Pile;
        if (pile?.Type != PileType.Deck)
        {
            return int.MaxValue;
        }

        for (int index = 0; index < pile.Cards.Count; index++)
        {
            if (ReferenceEquals(pile.Cards[index], deckCard))
            {
                return index;
            }
        }

        return int.MaxValue;
    }

    /// <summary>
    /// 多人端可能把一次本地随机升转落到不同的同名重复实例上。只要
    /// 各端选中的卡牌种类相同，组内转数多重集仍然相同；把较高转数
    /// 固定分配给较小的永久牌组槽位，即可在进入下一场战斗前恢复同一
    /// 实例身份，同时不改变玩家实际拥有的卡牌种类与转数总量。
    /// </summary>
    internal static int CanonicalizeDeckGuRanks(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);

        return CanonicalizeGuRanks(
            player.Deck.Cards,
            static card =>
            {
                int deckIndex = GetDeckCardIndex(card);
                return deckIndex == int.MaxValue
                    ? null
                    : (ulong)deckIndex;
            }
        );
    }

    /// <summary>
    /// NetCombatCardDb 建立编号后，以真正写入出牌协议的网络编号重新
    /// 规范化战斗克隆体。永久牌组槽位在两端相同仍不足以保证自定义
    /// 牌堆搬移前后的克隆实例相同，网络编号才是战斗内最终身份。
    /// </summary>
    internal static int CanonicalizeCombatGuRanks(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);

        if (player.PlayerCombatState == null)
        {
            return 0;
        }

        return CanonicalizeGuRanks(
            player.PlayerCombatState.AllCards,
            static card =>
            {
                uint networkId = GetCardNetworkId(card);
                return networkId == uint.MaxValue
                    ? null
                    : networkId;
            }
        );
    }

    private static int CanonicalizeGuRanks(
        IEnumerable<CardModel> candidates,
        Func<CardModel, ulong?> getStableIdentity
    )
    {
        Dictionary<
            GuCardIdentity,
            List<AbstractGuZhenRenCard>
        > groups = [];

        foreach (CardModel candidate in candidates)
        {
            if (candidate is not AbstractGuZhenRenCard guCard ||
                guCard is not IGuWormCard)
            {
                continue;
            }

            GuCardIdentity identity = new(
                guCard.Id.ToString(),
                guCard.CurrentUpgradeLevel,
                guCard.Enchantment?.Id.ToString() ?? string.Empty
            );
            if (!groups.TryGetValue(
                    identity,
                    out List<AbstractGuZhenRenCard>? cards
                ))
            {
                cards = [];
                groups.Add(identity, cards);
            }

            cards.Add(guCard);
        }

        int changedCount = 0;
        foreach (List<AbstractGuZhenRenCard> cards in groups.Values)
        {
            if (cards.Count < 2)
            {
                continue;
            }

            var identifiedCards = cards
                .Select(card => new
                {
                    Card = card,
                    StableIdentity = getStableIdentity(card),
                })
                .ToArray();

            // 身份缺失或重复时不能继承本地枚举顺序，否则修复本身也会
            // 制造不同步。正常的 DeckIndex/NetCombatCard ID 均唯一。
            if (identifiedCards.Any(static item =>
                    item.StableIdentity == null
                ) ||
                identifiedCards
                    .Select(static item => item.StableIdentity)
                    .Distinct()
                    .Count() != identifiedCards.Length)
            {
                continue;
            }

            AbstractGuZhenRenCard[] orderedCards = identifiedCards
                .OrderBy(static item => item.StableIdentity!.Value)
                .Select(static item => item.Card)
                .ToArray();
            int[] canonicalRanks = orderedCards
                .Select(static card => card.GuRank)
                .OrderByDescending(static rank => rank)
                .ToArray();

            for (int index = 0; index < orderedCards.Length; index++)
            {
                AbstractGuZhenRenCard card = orderedCards[index];
                int canonicalRank = canonicalRanks[index];
                if (card.GuRank == canonicalRank)
                {
                    continue;
                }

                card.InitializeGuRankFromSource(canonicalRank);
                changedCount++;
            }
        }

        return changedCount;
    }

    internal static Creature[] OrderCreatures(
        IEnumerable<Creature> creatures
    )
    {
        ArgumentNullException.ThrowIfNull(creatures);

        return creatures
            .OrderBy(creature =>
                creature.CombatId.HasValue ? 0 : 1
            )
            .ThenBy(creature =>
                creature.CombatId ?? uint.MaxValue
            )
            .ToArray();
    }
}
