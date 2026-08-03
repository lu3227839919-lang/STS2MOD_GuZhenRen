using GuZhenRen.Cards;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRen.Powers.XueDao;

/// <summary>
/// 血道资源与专属减益的公共入口。只有血道蛊虫牌能够主动获得、
/// 消耗血元或施加血印、流血；减益后续的自然结算由其施加者归属。
/// </summary>
public static class XueDaoPowerSystem
{
    public static bool IsXueDaoGuCard(CardModel? card)
    {
        return card is IGuWormCard &&
            card.Tags.Contains(GuZhenRenTags.XueDao);
    }

    public static int GetXueYuan(CardModel sourceCard)
    {
        if (!IsXueDaoGuCard(sourceCard) || sourceCard.IsCanonical)
        {
            return 0;
        }

        return sourceCard.Owner.Creature
            .GetPower<XueYuanPower>()?.Amount ?? 0;
    }

    public static async Task<int> GainXueYuan(
        PlayerChoiceContext choiceContext,
        CardModel sourceCard,
        int amount
    )
    {
        if (!IsXueDaoGuCard(sourceCard) ||
            sourceCard.IsCanonical)
        {
            return 0;
        }

        return await GainXueYuanInternal(
            choiceContext,
            sourceCard.Owner.Creature,
            amount,
            sourceCard
        );
    }

    internal static async Task<int> GainXueYuanFromEffect(
        PlayerChoiceContext choiceContext,
        Creature? applier,
        int amount
    )
    {
        if (applier?.Player == null ||
            applier.CombatState == null)
        {
            return 0;
        }

        return await GainXueYuanInternal(
            choiceContext,
            applier,
            amount,
            cardSource: null
        );
    }

    private static async Task<int> GainXueYuanInternal(
        PlayerChoiceContext choiceContext,
        Creature owner,
        int amount,
        CardModel? cardSource
    )
    {
        if (amount <= 0 || owner.CombatState == null)
        {
            return 0;
        }

        int before = owner.GetPower<XueYuanPower>()?.Amount ?? 0;
        int requested = Math.Min(
            amount,
            Math.Max(0, XueYuanPower.MaximumAmount - before)
        );

        if (requested <= 0)
        {
            return 0;
        }

        await PowerCmd.Apply<XueYuanPower>(
            choiceContext,
            owner,
            requested,
            owner,
            cardSource
        );

        int after = owner.GetPower<XueYuanPower>()?.Amount ?? 0;
        return Math.Max(0, after - before);
    }

    public static async Task<bool> TrySpendXueYuan(
        PlayerChoiceContext choiceContext,
        CardModel sourceCard,
        int amount
    )
    {
        if (amount <= 0)
        {
            return true;
        }

        if (!IsXueDaoGuCard(sourceCard) ||
            sourceCard.IsCanonical ||
            sourceCard.Owner.Creature.GetPower<XueYuanPower>() is not
                { } power ||
            power.Amount < amount)
        {
            return false;
        }

        int before = power.Amount;
        await PowerCmd.ModifyAmount(
            choiceContext,
            power,
            -amount,
            sourceCard.Owner.Creature,
            sourceCard
        );

        int after = sourceCard.Owner.Creature
            .GetPower<XueYuanPower>()?.Amount ?? 0;
        return before - after == amount;
    }

    public static async Task<bool> ApplyXueYin(
        PlayerChoiceContext choiceContext,
        CardModel sourceCard,
        Creature target,
        int amount
    )
    {
        if (!CanApplyBloodDebuff(sourceCard, target, amount))
        {
            return false;
        }

        return await PowerCmd.Apply<XueYinPower>(
            choiceContext,
            target,
            amount,
            sourceCard.Owner.Creature,
            sourceCard
        ) != null;
    }

    public static async Task<bool> ApplyLiuXue(
        PlayerChoiceContext choiceContext,
        CardModel sourceCard,
        Creature target,
        int amount
    )
    {
        if (!CanApplyBloodDebuff(sourceCard, target, amount))
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

    public static async Task<bool> ApplyNextTurnRecovery(
        PlayerChoiceContext choiceContext,
        CardModel sourceCard,
        int amount
    )
    {
        if (amount <= 0 ||
            !IsXueDaoGuCard(sourceCard) ||
            sourceCard.IsCanonical ||
            sourceCard.Owner.Creature.CombatState == null)
        {
            return false;
        }

        Creature owner = sourceCard.Owner.Creature;
        return await PowerCmd.Apply<XueQiRecoveryPower>(
            choiceContext,
            owner,
            amount,
            owner,
            sourceCard
        ) != null;
    }

    private static bool CanApplyBloodDebuff(
        CardModel sourceCard,
        Creature target,
        int amount
    )
    {
        return amount > 0 &&
            IsXueDaoGuCard(sourceCard) &&
            !sourceCard.IsCanonical &&
            target.IsEnemy &&
            ReferenceEquals(
                sourceCard.Owner.Creature.CombatState,
                target.CombatState
            );
    }
}
