using GuZhenRen.Cards;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRen.Powers.GuangDao;

/// <summary>
/// 光道 Power 的唯一公共调用入口。
/// 所有会获得、消耗光辉或施加照破的卡牌都必须带有 GuangDao 标签；
/// 非光道卡即使误调用也只会得到失败结果，不会改变战斗状态。
/// </summary>
public static class GuangDaoPowerSystem
{
    public static bool IsGuangDaoCard(CardModel? card)
    {
        return card?.Tags.Contains(GuZhenRenTags.GuangDao) == true;
    }

    /// <summary>
    /// 由光道卡获得光辉；返回本次实际增加的层数。
    /// </summary>
    public static async Task<int> GainGuangHui(
        PlayerChoiceContext choiceContext,
        CardModel sourceCard,
        int amount
    )
    {
        if (amount <= 0 ||
            !IsGuangDaoCard(sourceCard) ||
            sourceCard.IsCanonical ||
            sourceCard.Owner.Creature.CombatState == null)
        {
            return 0;
        }

        Creature owner = sourceCard.Owner.Creature;
        int before = owner.GetPower<GuangHuiPower>()?.Amount ?? 0;
        int room = Math.Max(0, GuangHuiPower.MaximumAmount - before);
        int requested = Math.Min(amount, room);

        if (requested <= 0)
        {
            return 0;
        }

        await PowerCmd.Apply<GuangHuiPower>(
            choiceContext,
            owner,
            requested,
            owner,
            sourceCard
        );

        int after = owner.GetPower<GuangHuiPower>()?.Amount ?? 0;
        return Math.Max(0, after - before);
    }

    /// <summary>
    /// 由光道卡支付光辉。资源不足或来源不是光道卡时不发生变化。
    /// </summary>
    public static async Task<bool> TrySpendGuangHui(
        PlayerChoiceContext choiceContext,
        CardModel sourceCard,
        int amount
    )
    {
        if (amount <= 0)
        {
            return true;
        }

        if (!IsGuangDaoCard(sourceCard) ||
            sourceCard.IsCanonical ||
            sourceCard.Owner.Creature.GetPower<GuangHuiPower>() is not
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
            .GetPower<GuangHuiPower>()?.Amount ?? 0;
        return before - after == amount;
    }

    /// <summary>
    /// 由光道卡向敌人施加照破；Artifact 等原生规则仍可阻止减益。
    /// </summary>
    public static async Task<bool> ApplyZhaoPo(
        PlayerChoiceContext choiceContext,
        CardModel sourceCard,
        Creature target,
        int amount
    )
    {
        if (amount <= 0 ||
            !IsGuangDaoCard(sourceCard) ||
            sourceCard.IsCanonical ||
            !target.IsEnemy ||
            !ReferenceEquals(
                sourceCard.Owner.Creature.CombatState,
                target.CombatState
            ))
        {
            return false;
        }

        ZhaoPoPower? applied = await PowerCmd.Apply<ZhaoPoPower>(
            choiceContext,
            target,
            amount,
            sourceCard.Owner.Creature,
            sourceCard
        );

        return applied != null;
    }

    internal static async Task EnsureZheGuang(Player player)
    {
        if (player.Creature.CombatState == null ||
            player.Creature.GetPower<ZheGuangPower>() != null)
        {
            return;
        }

        await PowerCmd.Apply<ZheGuangPower>(
            new ThrowingPlayerChoiceContext(),
            player.Creature,
            1,
            player.Creature,
            cardSource: null,
            silent: true
        );
    }
}
