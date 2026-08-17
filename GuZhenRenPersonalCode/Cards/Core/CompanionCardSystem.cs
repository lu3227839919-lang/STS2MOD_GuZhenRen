using GuZhenRen.Characters;
using GuZhenRen.Multiplayer;

using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRen.Cards;

/// <summary>
/// 会在战斗开始时生成伴生普通牌的蛊虫契约。
/// 力道兽力蛊（ILiDaoBeastGuCard）与宙道伴生蛊
/// （IZhouDaoCompanionGuCard）都实现该接口。
/// </summary>
public interface ICompanionSourceGuCard : IGuWormCard
{
    /// <summary>与来源蛊一一对应的伴生普通牌类型。</summary>
    Type CompanionCardType { get; }
}

/// <summary>
/// 战斗内伴生普通牌的公共契约（卡面转数跟随来源蛊）。
/// </summary>
public interface ICompanionCard
{
    /// <summary>本伴生牌对应的来源蛊类型。</summary>
    Type SourceGuType { get; }
}

/// <summary>
/// 在原生首次抽牌前，为伴生蛊生成战斗内伴生牌。
/// 伴生牌不进入永久牌组，直接继承来源蛊转数。
///
/// 两种配对模式：
/// - <see cref="CompanionPairingMode.OnePerSourceCard"/>（力道）：
///   每张来源蛊一张伴生牌，按永久牌组槽位一一对应；
/// - <see cref="CompanionPairingMode.OnePerSourceType"/>（宙道）：
///   每种来源蛊一张伴生牌，同名蛊并存时取最高转数。
/// </summary>
public static class CompanionCardSystem
{
    public enum CompanionPairingMode
    {
        /// <summary>每张来源蛊生成一张伴生牌（力道：按槽位一一对应）。</summary>
        OnePerSourceCard,

        /// <summary>每种来源蛊生成一张伴生牌（宙道：同名蛊取最高转数）。</summary>
        OnePerSourceType,
    }

    /// <summary>
    /// 此方法必须在 NetCombatCardDb.StartCombat 原方法之前调用，使新生成
    /// 的伴生牌与其他起始牌一同获得原生战斗网络编号，随后才允许抽牌。
    /// </summary>
    /// <param name="owner">要生成伴生牌的玩家。</param>
    /// <param name="mode">配对模式（力道一一对应 / 宙道每类一张）。</param>
    /// <param name="tryGetCompanionCardType">
    /// 从一张战斗牌取得其伴生牌类型；非来源蛊返回 null。
    /// </param>
    /// <param name="isCompanionCard">判断一张战斗牌是否为已生成的伴生牌。</param>
    /// <param name="companionKindName">日志/错误信息中使用的流派名（如"力道"）。</param>
    internal static int GenerateForCombat(
        Player owner,
        CompanionPairingMode mode,
        Func<AbstractGuZhenRenCard, Type?> tryGetCompanionCardType,
        Func<CardModel, bool> isCompanionCard,
        string companionKindName
    )
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(tryGetCompanionCardType);
        ArgumentNullException.ThrowIfNull(isCompanionCard);
        ArgumentNullException.ThrowIfNull(companionKindName);

        if (owner.PlayerCombatState is not { } combatState)
        {
            return 0;
        }

        CardModel[] combatCards = combatState.AllCards
            .Where(static card =>
                !card.HasBeenRemovedFromState &&
                card.Pile != null)
            .ToArray();

        var sourceGroups = combatCards
            .OfType<AbstractGuZhenRenCard>()
            .Select(card => (
                Card: card,
                CompanionType: tryGetCompanionCardType(card)
            ))
            .Where(item => item.CompanionType != null)
            .Select(item => (
                item.Card,
                CompanionType: item.CompanionType!
            ))
            .GroupBy(item => item.CompanionType)
            .OrderBy(
                static group => group.Key.FullName,
                StringComparer.Ordinal
            )
            .ToArray();

        if (sourceGroups.Length == 0)
        {
            return 0;
        }

        CardPile drawPile = PileType.Draw.GetPile(owner);
        int generatedCount = 0;

        foreach (var sourceGroup in sourceGroups)
        {
            generatedCount += mode ==
                CompanionPairingMode.OnePerSourceCard
                    ? GenerateOnePerSourceCard(
                        owner,
                        combatCards,
                        sourceGroup,
                        drawPile,
                        isCompanionCard,
                        companionKindName
                    )
                    : GenerateOnePerSourceType(
                        owner,
                        combatCards,
                        sourceGroup,
                        drawPile,
                        isCompanionCard,
                        companionKindName
                    );
        }

        if (generatedCount > 0)
        {
            drawPile.InvokeContentsChanged();
        }

        return generatedCount;
    }

    /// <summary>
    /// 力道模式：每张来源蛊一张伴生牌。已存在的战斗实例会被复用并
    /// 重新校准转数，不会重复生成；缺失的按来源蛊槽位顺序补齐。
    /// </summary>
    private static int GenerateOnePerSourceCard(
        Player owner,
        CardModel[] combatCards,
        IGrouping<Type, (AbstractGuZhenRenCard Card, Type CompanionType)>
            sourceGroup,
        CardPile drawPile,
        Func<CardModel, bool> isCompanionCard,
        string companionKindName
    )
    {
        AbstractGuZhenRenCard[] sourceCards = sourceGroup
            .Select(item => item.Card)
            .OrderBy(GuZhenRenDeterminism.GetDeckCardIndex)
            .ThenBy(
                static card => card.Id.ToString(),
                StringComparer.Ordinal
            )
            .ToArray();

        AbstractGuZhenRenCard[] companions = combatCards
            .Where(card =>
                card.GetType() == sourceGroup.Key &&
                isCompanionCard(card))
            .OfType<AbstractGuZhenRenCard>()
            .OrderBy(GuZhenRenDeterminism.GetDeckCardIndex)
            .ThenBy(GuZhenRenDeterminism.GetCardNetworkId)
            .ToArray();

        int pairedCount = Math.Min(sourceCards.Length, companions.Length);
        for (int index = 0; index < pairedCount; index++)
        {
            SetCompanionRank(companions[index], sourceCards[index].GuRank);
        }

        int generatedCount = 0;
        for (int index = companions.Length;
             index < sourceCards.Length;
             index++)
        {
            CardModel companion = CreateCombatCard(owner, sourceGroup.Key);

            try
            {
                if (companion is not AbstractGuZhenRenCard rankedCompanion)
                {
                    throw new InvalidOperationException(
                        $"{companionKindName}伴生牌 " +
                        $"{sourceGroup.Key.Name} 不支持转数。"
                    );
                }

                SetCompanionRank(
                    rankedCompanion,
                    sourceCards[index].GuRank
                );
                drawPile.AddInternal(companion, silent: true);
                generatedCount++;
            }
            catch
            {
                companion.RemoveFromState();
                throw;
            }
        }

        return generatedCount;
    }

    /// <summary>
    /// 宙道模式：每种来源蛊一张伴生牌，转数取该组最高转数。
    /// </summary>
    private static int GenerateOnePerSourceType(
        Player owner,
        CardModel[] combatCards,
        IGrouping<Type, (AbstractGuZhenRenCard Card, Type CompanionType)>
            sourceGroup,
        CardPile drawPile,
        Func<CardModel, bool> isCompanionCard,
        string companionKindName
    )
    {
        int rank = sourceGroup.Max(item => item.Card.GuRank);

        AbstractGuZhenRenCard? companion = combatCards
            .Where(card =>
                card.GetType() == sourceGroup.Key &&
                isCompanionCard(card))
            .OfType<AbstractGuZhenRenCard>()
            .OrderBy(GuZhenRenDeterminism.GetDeckCardIndex)
            .FirstOrDefault();

        if (companion != null)
        {
            SetCompanionRank(companion, rank);
            return 0;
        }

        CardModel created = CreateCombatCard(owner, sourceGroup.Key);
        try
        {
            if (created is not AbstractGuZhenRenCard rankedCompanion)
            {
                throw new InvalidOperationException(
                    $"{companionKindName}伴生牌 " +
                    $"{sourceGroup.Key.Name} 不支持转数。"
                );
            }

            SetCompanionRank(rankedCompanion, rank);
            drawPile.AddInternal(created, silent: true);
            return 1;
        }
        catch
        {
            created.RemoveFromState();
            throw;
        }
    }

    private static void SetCompanionRank(
        AbstractGuZhenRenCard companion,
        int rank
    )
    {
        if (companion.GuRank != rank)
        {
            companion.InitializeGuRankFromSource(rank);
        }

        // 网络反序列化或旧存档实例即使转数相同，也可能仍保留一转数值。
        companion.RefreshGuRankDerivedState();
    }

    private static CardModel CreateCombatCard(
        Player owner,
        Type cardType
    )
    {
        CardModel canonical = ModelDb
            .CardPool<GuZhenRenCardPool>()
            .AllCards
            .Single(card => card.GetType() == cardType);

        return owner.Creature.CombatState!.CreateCard(
            canonical,
            owner
        );
    }
}
