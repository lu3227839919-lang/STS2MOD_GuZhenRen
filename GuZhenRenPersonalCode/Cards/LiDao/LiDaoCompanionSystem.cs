using GuZhenRen.Characters;
using GuZhenRen.Multiplayer;

using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRen.Cards.LiDao;

/// <summary>
/// 在原生首次抽牌前，为每只永久力道蛊生成一张战斗内伴生牌。
/// 伴生牌不进入永久牌组；同类型蛊与伴生牌按永久牌组槽位一一对应，
/// 并直接继承各自来源蛊的当前转数。
/// </summary>
public static class LiDaoCompanionSystem
{
    /// <summary>
    /// 此方法必须在 NetCombatCardDb.StartCombat 原方法之前调用，使新生成
    /// 的伴生牌与其他起始牌一同获得原生战斗网络编号，随后才允许抽牌。
    /// </summary>
    internal static int GenerateForCombat(Player owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

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
            .Where(static card => card is ILiDaoBeastGuCard)
            .GroupBy(card =>
                ((ILiDaoBeastGuCard)card).CompanionCardType)
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
            AbstractGuZhenRenCard[] sourceCards = sourceGroup
                .OrderBy(GuZhenRenDeterminism.GetDeckCardIndex)
                .ThenBy(
                    static card => card.Id.ToString(),
                    StringComparer.Ordinal
                )
                .ToArray();

            // 兼容同一战斗恢复流程以及旧存档已经克隆出的伴生牌：
            // 已存在的战斗实例会被复用并重新校准转数，不会重复生成。
            AbstractGuZhenRenCard[] companions = combatCards
                .Where(card =>
                    card.GetType() == sourceGroup.Key &&
                    card is ILiDaoCompanionCard)
                .OfType<AbstractGuZhenRenCard>()
                .OrderBy(GuZhenRenDeterminism.GetDeckCardIndex)
                .ThenBy(GuZhenRenDeterminism.GetCardNetworkId)
                .ToArray();

            int pairedCount = Math.Min(sourceCards.Length, companions.Length);
            for (int index = 0; index < pairedCount; index++)
            {
                SetCompanionRank(companions[index], sourceCards[index].GuRank);
            }

            for (int index = companions.Length;
                 index < sourceCards.Length;
                 index++)
            {
                CardModel companion = CreateCombatCard(
                    owner,
                    sourceGroup.Key
                );

                try
                {
                    if (companion is not AbstractGuZhenRenCard rankedCompanion)
                    {
                        throw new InvalidOperationException(
                            $"力道伴生牌 {sourceGroup.Key.Name} 不支持转数。"
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
        }

        if (generatedCount > 0)
        {
            drawPile.InvokeContentsChanged();
        }

        return generatedCount;
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
