using GuZhenRen.Cards;
using GuZhenRen.Cards.XueDao;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRen.Powers.XueDao;

/// <summary>
/// 血道资源、寄生、遗骸与专属减益的公共入口。
/// </summary>
public static class XueDaoPowerSystem
{
    public static bool IsXueDaoGuCard(CardModel? card)
    {
        return card is IGuWormCard &&
            card.Tags.Contains(GuZhenRenTags.XueDao);
    }

    /// <summary>
    /// 血道效果牌包括血道蛊虫、血道杀招/衍生牌，以及被血道寄生的
    /// 普通宿主牌。该判定用于流血、血印与取骸的统一归属。
    /// </summary>
    public static bool IsXueDaoEffectCard(CardModel? card)
    {
        return card != null &&
            (card.Tags.Contains(GuZhenRenTags.XueDao) ||
             XueDaoParasiteSystem.HasParasite(card));
    }

    public static int GetXueYuan(CardModel sourceCard)
    {
        if (sourceCard.IsCanonical)
        {
            return 0;
        }

        return GetXueYuan(sourceCard.Owner.Creature);
    }

    public static int GetXueYuan(Creature owner) =>
        owner.GetPower<XueYuanPower>()?.Amount ?? 0;

    public static int GetXueLu(Creature owner) =>
        owner.GetPower<XueLuPower>()?.Amount ?? 0;

    public static async Task<int> GainXueYuan(
        PlayerChoiceContext choiceContext,
        CardModel sourceCard,
        int amount
    )
    {
        if (sourceCard.IsCanonical ||
            !IsXueDaoEffectCard(sourceCard))
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

    public static async Task<int> GainXueYuanFromCardEffect(
        PlayerChoiceContext choiceContext,
        CardModel sourceCard,
        int amount
    )
    {
        if (sourceCard.IsCanonical ||
            sourceCard.Owner.Creature.CombatState == null)
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

        int before = GetXueYuan(owner);
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

        return Math.Max(0, GetXueYuan(owner) - before);
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

        if (sourceCard.IsCanonical ||
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

        return before - GetXueYuan(sourceCard.Owner.Creature) == amount;
    }

    public static async Task<(int Added, int Overflow)> GainXueLuOrOverflow(
        PlayerChoiceContext choiceContext,
        CardModel sourceCard,
        int amount
    )
    {
        if (amount <= 0 || sourceCard.IsCanonical)
        {
            return (0, 0);
        }

        Creature owner = sourceCard.Owner.Creature;
        int before = GetXueLu(owner);
        int room = Math.Max(0, XueLuPower.MaximumAmount - before);
        int toAdd = Math.Min(room, amount);

        if (toAdd > 0)
        {
            await PowerCmd.Apply<XueLuPower>(
                choiceContext,
                owner,
                toAdd,
                owner,
                sourceCard
            );
        }

        return (Math.Max(0, GetXueLu(owner) - before), amount - toAdd);
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

    public static LiuXuePower? GetLiuXue(
        Creature target,
        Creature applier
    ) => target.GetPowerInstances<LiuXuePower>()
        .FirstOrDefault(power => ReferenceEquals(power.Applier, applier));

    public static async Task SetLiuXueAmount(
        PlayerChoiceContext choiceContext,
        CardModel sourceCard,
        Creature target,
        int amount
    )
    {
        LiuXuePower? existing = GetLiuXue(
            target,
            sourceCard.Owner.Creature
        );

        if (existing != null)
        {
            int delta = amount - existing.Amount;
            if (delta != 0)
            {
                await PowerCmd.ModifyAmount(
                    choiceContext,
                    existing,
                    delta,
                    sourceCard.Owner.Creature,
                    sourceCard
                );
            }
            return;
        }

        if (amount > 0)
        {
            await ApplyLiuXue(
                choiceContext,
                sourceCard,
                target,
                amount
            );
        }
    }

    public static async Task<bool> ApplyNextTurnRecovery(
        PlayerChoiceContext choiceContext,
        CardModel sourceCard,
        int amount
    )
    {
        if (amount <= 0 ||
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
            IsXueDaoEffectCard(sourceCard) &&
            !sourceCard.IsCanonical &&
            target.IsEnemy &&
            ReferenceEquals(
                sourceCard.Owner.Creature.CombatState,
                target.CombatState
            );
    }
}
