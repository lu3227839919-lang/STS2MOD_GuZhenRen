using GuZhenRen.Aperture;
using GuZhenRen.Cards.ImmortalEssence;

using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Cards.FreePlay;
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

    public static int CountSpentActivations(CardModel card)
    {
        ArgumentNullException.ThrowIfNull(card);

        if (card is not IGuWormCard guCard)
        {
            return 0;
        }

        return Math.Clamp(
            SpentActivationsState[card],
            0,
            Math.Max(0, guCard.MaxUses)
        );
    }

    public static int GetRemainingUses(CardModel card)
    {
        ArgumentNullException.ThrowIfNull(card);

        return card is IGuWormCard guCard
            ? Math.Max(
                0,
                guCard.MaxUses -
                CountSpentActivations(card)
            )
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
            Math.Max(0, guCard.MaxUses),
            CountSpentActivations(card) + 1
        );
    }

    public static void ResetUses(CardModel card)
    {
        ArgumentNullException.ThrowIfNull(card);
        SpentActivationsState[card] = 0;
    }

    public static bool CanUse(CardModel card)
    {
        ArgumentNullException.ThrowIfNull(card);

        if (card is not IGuWormCard guCard)
        {
            return true;
        }

        return guCard.MaxUses > 0 &&
               GetRemainingUses(card) > 0;
    }

    /// <summary>
    /// 判断下一次催动是否应免除基础费用。自动打出本身不在这里视为
    /// 免费，因为催动牌只是承载蛊虫结算，元气仍应正常支付。
    /// </summary>
    public static bool IsActivationFree(CardModel card)
    {
        ArgumentNullException.ThrowIfNull(card);

        return card is IGuWormCard &&
            (ApertureSystem.IsNextGuActivationFree(card) ||
             FreePlayBindingRegistry.IsCardFreeForUpcomingPlay(card));
    }

    /// <summary>
    /// 创建蛊虫本次催动的次级资源支付计划。该计划随后会绑定到
    /// AutoPlay 生成的 CardPlay，避免自动打出把固定元气费用误判为免费。
    /// </summary>
    public static SecondaryResourcePaymentPlan
        CreateActivationPaymentPlan(CardModel card)
    {
        ArgumentNullException.ThrowIfNull(card);

        return SecondaryResourcePaymentResolver.Plan(
            card,
            isFree: IsActivationFree(card),
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

        if (plan.HasLines)
        {
            await SecondaryResourcePaymentResolver.Commit(
                plan,
                source: card
            );
        }

        return true;
    }
}
