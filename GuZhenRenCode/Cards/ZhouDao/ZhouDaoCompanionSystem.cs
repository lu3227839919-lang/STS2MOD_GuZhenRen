using System.Threading;

using GuZhenRen.Characters;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRen.Cards.ZhouDao;

/// <summary>
/// 宙道伴生牌只保留每种一张；同名蛊并存时，伴生牌转数取其中最高转数。
/// 这样能力牌不会因为玩家拿到多只同名蛊而污染普通牌组。
/// </summary>
public static class ZhouDaoCompanionSystem
{
    private static readonly AsyncLocal<int> MutationDepth = new();

    internal static async Task EnsureForGuAsync(CardModel guCard)
    {
        if (MutationDepth.Value > 0 ||
            guCard is not IZhouDaoCompanionGuCard companionGu ||
            guCard.Pile?.Type != PileType.Deck)
        {
            return;
        }

        Player owner = guCard.Owner;
        Type companionType = companionGu.CompanionCardType;
        AbstractGuZhenRenCard? existing = owner.Deck.Cards
            .Where(card => card.GetType() == companionType)
            .OfType<AbstractGuZhenRenCard>()
            .FirstOrDefault();

        if (existing == null)
        {
            MutationDepth.Value++;
            try
            {
                CardModel companion = CreateDeckCard(owner, companionType);
                CardPileAddResult result = await CardPileCmd.Add(
                    companion,
                    PileType.Deck
                );

                if (!result.success)
                {
                    companion.RemoveFromState();
                    throw new InvalidOperationException(
                        $"无法为 {guCard.Id} 加入宙道伴生牌 {companionType.Name}。"
                    );
                }
            }
            finally
            {
                MutationDepth.Value--;
            }
        }

        SyncCompanionRank(owner, companionType);
    }

    internal static async Task BeforeGuRemovedAsync(CardModel guCard)
    {
        if (MutationDepth.Value > 0 ||
            guCard is not IZhouDaoCompanionGuCard companionGu ||
            guCard.Pile?.Type != PileType.Deck)
        {
            return;
        }

        Player owner = guCard.Owner;
        Type companionType = companionGu.CompanionCardType;
        CardModel? companion = owner.Deck.Cards.FirstOrDefault(card =>
            card.GetType() == companionType
        );
        if (companion == null)
        {
            return;
        }

        AbstractGuZhenRenCard[] remaining = owner.Deck.Cards
            .Where(card =>
                !ReferenceEquals(card, guCard) &&
                card is IZhouDaoCompanionGuCard candidate &&
                candidate.CompanionCardType == companionType)
            .OfType<AbstractGuZhenRenCard>()
            .ToArray();

        if (remaining.Length == 0)
        {
            MutationDepth.Value++;
            try
            {
                await CardPileCmd.RemoveFromDeck(
                    companion,
                    showPreview: false
                );
            }
            finally
            {
                MutationDepth.Value--;
            }
            return;
        }

        if (companion is AbstractGuZhenRenCard rankedCompanion)
        {
            SetCompanionRank(
                rankedCompanion,
                remaining.Max(static card => card.GuRank)
            );
        }
    }

    internal static void SyncForGu(AbstractGuZhenRenCard guCard)
    {
        if (MutationDepth.Value > 0 ||
            guCard is not IZhouDaoCompanionGuCard companionGu ||
            guCard.Owner is not { } owner)
        {
            return;
        }

        SyncCompanionRank(owner, companionGu.CompanionCardType);
    }

    internal static void SyncFromCompanion(AbstractGuZhenRenCard companion)
    {
        if (MutationDepth.Value > 0 ||
            companion is not IZhouDaoCompanionCard ||
            companion.Owner is not { } owner)
        {
            return;
        }

        SyncCompanionRank(owner, companion.GetType());
    }

    private static void SyncCompanionRank(Player owner, Type companionType)
    {
        if (MutationDepth.Value > 0)
        {
            return;
        }

        AbstractGuZhenRenCard? companion = owner.Deck.Cards
            .Where(card => card.GetType() == companionType)
            .OfType<AbstractGuZhenRenCard>()
            .FirstOrDefault();
        if (companion == null)
        {
            return;
        }

        int? rank = owner.Deck.Cards
            .Where(card =>
                card is IZhouDaoCompanionGuCard gu &&
                gu.CompanionCardType == companionType)
            .OfType<AbstractGuZhenRenCard>()
            .Select(static card => (int?)card.GuRank)
            .Max();

        if (rank is { } value)
        {
            SetCompanionRank(companion, value);
        }
    }

    private static void SetCompanionRank(
        AbstractGuZhenRenCard companion,
        int rank
    )
    {
        if (companion.GuRank == rank)
        {
            return;
        }

        MutationDepth.Value++;
        try
        {
            companion.InitializeGuRankFromSource(rank);
        }
        finally
        {
            MutationDepth.Value--;
        }
    }

    private static CardModel CreateDeckCard(Player owner, Type cardType)
    {
        CardModel canonical = ModelDb
            .CardPool<GuZhenRenCardPool>()
            .AllCards
            .Single(card => card.GetType() == cardType);

        return owner.RunState.CreateCard(canonical, owner);
    }
}
