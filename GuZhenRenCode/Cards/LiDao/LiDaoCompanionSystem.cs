using System.Threading;

using GuZhenRen.Characters;
using GuZhenRen.Multiplayer;

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

            // 伴生牌与蛊一一对应：创建时直接复制蛊的当前转数，
            // 保证卡面转数第一时间与对应蛊一致。
            if (companion is AbstractGuZhenRenCard companionCard &&
                guCard is AbstractGuZhenRenCard sourceGu &&
                companionCard.GuRank != sourceGu.GuRank)
            {
                MutationDepth.Value++;
                try
                {
                    companionCard.InitializeGuRankFromSource(sourceGu.GuRank);
                }
                finally
                {
                    MutationDepth.Value--;
                }
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
                if (companion is AbstractGuZhenRenCard companionCard)
                {
                    // 合练回滚补齐的伴生牌也跟随同类型蛊转数。
                    SyncRankFromGuToCompanions(companionCard);
                }
            }
        }

        owner.Deck.InvokeContentsChanged();
    }

    /// <summary>
    /// 蛊转数变化（升转/读档/复制）后同步全部同类型伴生牌转数。
    /// 由 AbstractLiDaoGuCard.OnGuRankChanged 调用。
    /// </summary>
    internal static void SyncCompanionsForGu(
        AbstractGuZhenRenCard guCard
    )
    {
        ArgumentNullException.ThrowIfNull(guCard);

        if (guCard.IsCanonical ||
            guCard is not ILiDaoTrainingGuCard trainingGu ||
            guCard.Owner is not { } owner ||
            MutationDepth.Value > 0)
        {
            return;
        }

        Type companionType = trainingGu.CompanionCardType;

        AbstractGuZhenRenCard[] companions = owner.Deck.Cards
            .Where(card => card.GetType() == companionType)
            .OfType<AbstractGuZhenRenCard>()
            .OrderBy(GuZhenRenDeterminism.GetCardNetworkId)
            .ToArray();

        foreach (AbstractGuZhenRenCard companion in companions)
        {
            SyncRankFromGuToCompanions(companion);
        }
    }

    /// <summary>
    /// 伴生牌转数跟随对应力道蛊：按稳定网络顺序把 owner 牌组中
    /// 同类型蛊的当前转数同步到同序伴生牌。canonical 实例没有
    /// owner（图鉴/卡池预览），直接跳过。
    /// </summary>
    internal static void SyncRankFromGuToCompanions(
        AbstractGuZhenRenCard companion
    )
    {
        ArgumentNullException.ThrowIfNull(companion);

        if (companion.IsCanonical ||
            companion is not ILiDaoCompanionCard liDaoCompanion ||
            companion.Owner is not { } owner ||
            MutationDepth.Value > 0)
        {
            return;
        }

        Type guType = liDaoCompanion.TrainedGuType;

        // 同类型蛊与同类型伴生牌都按网络 ID 排序，一一配对同步转数，
        // 保证多张同名蛊/伴生牌并存时仍各取各的对应蛊转数。
        AbstractGuZhenRenCard[] guRanks = owner.Deck.Cards
            .Where(card =>
                card is ILiDaoTrainingGuCard trainingGu &&
                trainingGu.CompanionCardType == companion.GetType())
            .OfType<AbstractGuZhenRenCard>()
            .OrderBy(GuZhenRenDeterminism.GetCardNetworkId)
            .ToArray();

        AbstractGuZhenRenCard[] companions = owner.Deck.Cards
            .Where(card => card.GetType() == companion.GetType())
            .OfType<AbstractGuZhenRenCard>()
            .OrderBy(GuZhenRenDeterminism.GetCardNetworkId)
            .ToArray();

        for (int index = 0; index < companions.Length; index++)
        {
            if (index >= guRanks.Length ||
                ReferenceEquals(companions[index], companion) is false)
            {
                continue;
            }

            int sourceRank = guRanks[index].GuRank;
            if (companions[index].GuRank != sourceRank)
            {
                MutationDepth.Value++;
                try
                {
                    companions[index].InitializeGuRankFromSource(sourceRank);
                }
                finally
                {
                    MutationDepth.Value--;
                }
            }
            return;
        }

        // 牌组中还没有对应蛊（例如蛊尚在生成流程中）时，保持当前转数。
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
