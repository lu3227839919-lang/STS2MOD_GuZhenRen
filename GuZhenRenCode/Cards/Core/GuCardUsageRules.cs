using GuZhenRen.Cards.ImmortalEssence;

using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Utils;

namespace GuZhenRen.Cards;

/// <summary>
/// 蛊虫牌的持久催动次数规则。每次出牌序列只登记一次，Replay 不额外
/// 消耗次数；剩余次数跨回合保存，直到蛊虫进入恢复堆并完成恢复。
/// </summary>
public static class GuCardUsageRules
{
    private static readonly SavedAttachedState<CardModel, int>
        SpentActivationsState = new(
            "gu_zhen_ren.gu_spent_activations",
            () => 0
        );

    public static int GetRemainingUses(CardModel card)
    {
        ArgumentNullException.ThrowIfNull(card);

        return card is IGuWormCard guCard
            ? GetRemainingUses(card, guCard)
            : int.MaxValue;
    }

    public static void RegisterActivation(CardModel card)
    {
        ArgumentNullException.ThrowIfNull(card);

        if (card is not IGuWormCard guCard)
        {
            return;
        }

        SpentActivationsState[card] = Math.Min(
            GetMaximumUses(guCard),
            CountSpentActivations(card, guCard) + 1
        );
    }

    public static void ResetUses(CardModel card)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (card is IGuWormCard)
        {
            SpentActivationsState[card] = 0;
        }
    }

    public static bool CanUse(CardModel card)
    {
        ArgumentNullException.ThrowIfNull(card);

        if (card is not IGuWormCard guCard)
        {
            return true;
        }

        return GetRemainingUses(card, guCard) > 0;
    }

    /// <summary>
    /// 创建蛊虫本次催动的次级资源支付计划。催动始终支付实际费用；
    /// 提交后的计划会绑定到 AutoPlay 生成的 CardPlay。
    /// </summary>
    internal static SecondaryResourcePaymentPlan
        CreateActivationPaymentPlan(CardModel card)
    {
        ArgumentNullException.ThrowIfNull(card);

        return SecondaryResourcePaymentResolver.Plan(
            card,
            isFree: false,
            source: card
        );
    }

    public static bool CanActivate(CardModel card)
    {
        ArgumentNullException.ThrowIfNull(card);

        return card is IGuWormCard &&
               CanUse(card) &&
               ImmortalEssenceSystem.CanPayForActivation(card) &&
               CreateActivationPaymentPlan(card).IsAffordable;
    }

    public static async Task<bool> CommitActivationPayment(
        CardModel card
    )
    {
        ArgumentNullException.ThrowIfNull(card);

        SecondaryResourcePaymentPlan plan =
            CreateActivationPaymentPlan(card);

        if (!plan.IsAffordable)
        {
            return false;
        }

        await SecondaryResourcePaymentResolver.Commit(
            plan,
            source: card
        );

        return true;
    }

    private static int CountSpentActivations(
        CardModel card,
        IGuWormCard guCard
    )
    {
        return Math.Clamp(
            SpentActivationsState[card],
            0,
            GetMaximumUses(guCard)
        );
    }

    private static int GetRemainingUses(
        CardModel card,
        IGuWormCard guCard
    )
    {
        return Math.Max(
            0,
            GetMaximumUses(guCard) -
            CountSpentActivations(card, guCard)
        );
    }

    private static int GetMaximumUses(IGuWormCard guCard)
    {
        return Math.Max(0, guCard.MaxUses);
    }
}
