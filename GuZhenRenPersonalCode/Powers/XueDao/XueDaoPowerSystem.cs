using GuZhenRen.Cards;
using GuZhenRen.Cards.XueDao;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRen.Powers.XueDao;

public static class XueDaoPowerSystem
{
    public static bool IsXueDaoGuCard(CardModel? card) =>
        card is IGuWormCard &&
        card.Tags.Contains(GuZhenRenTags.XueDao);

    public static bool IsXueDaoEffectCard(CardModel? card) =>
        card != null &&
        (card.Tags.Contains(GuZhenRenTags.XueDao) ||
         XueDaoParasiteSystem.HasTriggeringParasite(card));

    public static async Task<bool> ApplyLiuXue(
        PlayerChoiceContext choiceContext,
        CardModel sourceCard,
        Creature target,
        int amount
    )
    {
        if (amount <= 0 ||
            sourceCard.IsCanonical ||
            !IsXueDaoEffectCard(sourceCard) ||
            !target.IsEnemy ||
            !ReferenceEquals(
                sourceCard.Owner.Creature.CombatState,
                target.CombatState
            ))
        {
            return false;
        }

        return await PowerCmd.Apply<LiuXuePower>(
            choiceContext,
            target,
            amount,
            sourceCard.Owner.Creature,
            sourceCard
        ) != null;
    }

    public static LiuXuePower? GetLiuXue(
        Creature target,
        Creature applier
    ) => target.GetPowerInstances<LiuXuePower>()
        .FirstOrDefault(power => ReferenceEquals(power.Applier, applier));
}
