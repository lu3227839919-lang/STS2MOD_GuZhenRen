using System.Threading;

using GuZhenRen.Characters;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRen.Cards.LiDao;

/// <summary>保持永久牌组中的力道蛊与伴生普通牌一一对应。</summary>
public static class LiDaoCompanionSystem
{
    private static readonly AsyncLocal<int> MutationDepth = new();

    internal static async Task EnsureForGuAsync(
        CardModel guCard
    )
    {
        if (MutationDepth.Value > 0 ||
            guCard is not ILiDaoTrainingGuCard trainingGu ||
            guCard.Pile?.Type != PileType.Deck)
        {
            return;
        }

        Player owner = guCard.Owner;
        Type companionType = trainingGu.CompanionCardType;
        int guCount = owner.Deck.Cards.Count(card =>
            card is ILiDaoTrainingGuCard candidate &&
            candidate.CompanionCardType == companionType
        );
        int companionCount = owner.Deck.Cards.Count(card =>
            card.GetType() == companionType
        );

        if (companionCount >= guCount)
        {
            return;
        }

        MutationDepth.Value++;
        try
        {
            CardModel companion = CreateDeckCard(
                owner,
                companionType
            );
            CardPileAddResult result = await CardPileCmd.Add(
                companion,
                PileType.Deck
            );

            if (!result.success)
            {
                companion.RemoveFromState();
                throw new InvalidOperationException(
                    $"无法为 {guCard.Id} 加入伴生牌 {companionType.Name}。"
                );
            }
        }
        finally
        {
            MutationDepth.Value--;
        }
    }

    internal static async Task RemoveOneForGuAsync(
        CardModel guCard
    )
    {
        if (MutationDepth.Value > 0 ||
            guCard is not ILiDaoTrainingGuCard trainingGu ||
            guCard.Pile?.Type != PileType.Deck)
        {
            return;
        }

        CardModel? companion = guCard.Owner.Deck.Cards
            .FirstOrDefault(card =>
                card.GetType() == trainingGu.CompanionCardType
            );

        if (companion == null)
        {
            return;
        }

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
    }

    /// <summary>
    /// 合练失败回滚使用同步牌堆恢复；此入口在不追加牌组历史的前提下
    /// 补齐被一并移除的伴生牌。
    /// </summary>
    internal static void RestoreMissingCompanions(Player owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        IGrouping<Type, ILiDaoTrainingGuCard>[] groups = owner.Deck.Cards
            .OfType<ILiDaoTrainingGuCard>()
            .GroupBy(gu => gu.CompanionCardType)
            .ToArray();

        foreach (IGrouping<Type, ILiDaoTrainingGuCard> group in groups)
        {
            int missing = group.Count() - owner.Deck.Cards.Count(card =>
                card.GetType() == group.Key
            );

            for (int index = 0; index < missing; index++)
            {
                CardModel companion = CreateDeckCard(owner, group.Key);
                owner.Deck.AddInternal(companion);
            }
        }

        owner.Deck.InvokeContentsChanged();
    }

    private static CardModel CreateDeckCard(
        Player owner,
        Type cardType
    )
    {
        CardModel canonical = ModelDb
            .CardPool<GuZhenRenCardPool>()
            .AllCards
            .Single(card => card.GetType() == cardType);

        return owner.RunState.CreateCard(canonical, owner);
    }
}
