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
    private readonly record struct GuDeckIdentity(
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

        Dictionary<
            GuDeckIdentity,
            List<AbstractGuZhenRenCard>
        > groups = [];

        // Deck.Cards 本身就是 NetDeckCard 使用的协议顺序；保持此插入
        // 顺序，后续即可直接把排序后的转数写回固定槽位。
        foreach (CardModel deckCard in player.Deck.Cards)
        {
            if (deckCard is not AbstractGuZhenRenCard guCard ||
                guCard is not IGuWormCard)
            {
                continue;
            }

            GuDeckIdentity identity = new(
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

            int[] canonicalRanks = cards
                .Select(static card => card.GuRank)
                .OrderByDescending(static rank => rank)
                .ToArray();

            for (int index = 0; index < cards.Count; index++)
            {
                AbstractGuZhenRenCard card = cards[index];
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
