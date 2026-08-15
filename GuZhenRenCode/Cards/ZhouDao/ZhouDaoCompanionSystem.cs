using GuZhenRen.Characters;
using GuZhenRen.Multiplayer;

using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRen.Cards.ZhouDao;

/// <summary>
/// 在原生首次抽牌前，为每种宙道伴生蛊生成一张战斗内伴生能力牌。
/// 伴生牌不进入永久牌组；同名蛊并存时，伴生牌转数取其中最高转数。
/// </summary>
public static class ZhouDaoCompanionSystem
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
            .Where(static card => card is IZhouDaoCompanionGuCard)
            .GroupBy(card =>
                ((IZhouDaoCompanionGuCard)card).CompanionCardType)
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
            int rank = sourceGroup.Max(static card => card.GuRank);
            AbstractGuZhenRenCard? companion = combatCards
                .Where(card =>
                    card.GetType() == sourceGroup.Key &&
                    card is IZhouDaoCompanionCard)
                .OfType<AbstractGuZhenRenCard>()
                .OrderBy(GuZhenRenDeterminism.GetDeckCardIndex)
                .FirstOrDefault();

            if (companion != null)
            {
                SetCompanionRank(companion, rank);
                continue;
            }

            CardModel created = CreateCombatCard(owner, sourceGroup.Key);
            try
            {
                if (created is not AbstractGuZhenRenCard rankedCompanion)
                {
                    throw new InvalidOperationException(
                        $"宙道伴生牌 {sourceGroup.Key.Name} 不支持转数。"
                    );
                }

                SetCompanionRank(rankedCompanion, rank);
                drawPile.AddInternal(created, silent: true);
                generatedCount++;
            }
            catch
            {
                created.RemoveFromState();
                throw;
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

        companion.RefreshGuRankDerivedState();
    }

    private static CardModel CreateCombatCard(Player owner, Type cardType)
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
