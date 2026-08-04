using System.Runtime.CompilerServices;

using GuZhenRen.Cards.ImmortalEssence;
using GuZhenRen.Combat;

using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Utils;

namespace GuZhenRen.Cards;

/// <summary>
/// 蛊虫牌的持久催动次数、资源支付和逐牌恢复规则。
/// 每次出牌序列只登记一次，Replay 不额外消耗次数、元气或仙元。
/// 恢复回合由每张蛊虫的 RecoveryDelayTurns 决定。
/// </summary>
public static class GuCardUsageRules
{
    private sealed class PreparedPaymentState
    {
        public int Count;
    }

    private static readonly SavedAttachedState<CardModel, int>
        SpentActivationsState = new(
            "gu_zhen_ren.gu_spent_activations",
            () => 0
        );

    // 0 表示尚未进入恢复；其他值是可恢复的回合编号。
    private static readonly SavedAttachedState<CardModel, int>
        RecoveryReadyTurnState = new(
            "gu_zhen_ren.gu_recovery_ready_turn",
            () => 0
        );

    // 元气可能在 AutoPlay 创建 CardPlay 之前预付。该状态只在当前进程
    // 的一次出牌链中使用，不写入存档；真正的资源数量仍由游戏同步。
    private static readonly ConditionalWeakTable<
        CardModel,
        PreparedPaymentState
    > PreparedPayments = new();

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
            RecoveryReadyTurnState[card] = 0;
            PreparedPayments.Remove(card);

            if (card is IGuRecoveryEffectSource recoverySource)
            {
                recoverySource.ResetRecoveryEffectState();
            }
        }
    }

    public static bool CanUse(CardModel card)
    {
        ArgumentNullException.ThrowIfNull(card);

        return card is not IGuWormCard guCard ||
            GetRemainingUses(card, guCard) > 0;
    }

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

        return card is IGuWormCard guCard &&
               CanUse(card) &&
               HasEnoughYuanQi(card, guCard) &&
               ImmortalEssenceSystem.CanPayForActivation(card) &&
               (
                   HasPreparedActivationPayment(card) ||
                   CreateActivationPaymentPlan(card).IsAffordable
               );
    }


    private static bool HasEnoughYuanQi(
        CardModel card,
        IGuWormCard guCard
    )
    {
        int cost = Math.Max(0, guCard.YuanQiCost);
        return cost == 0 ||
            SecondaryResourceCmd.Get(
                card.Owner,
                YuanQiSystem.ResourceId
            ) >= cost;
    }

    /// <summary>
    /// 在 AutoPlay 前预付元气，并登记为本次出牌序列已付款。
    /// Replay 后续段不会再次调用本方法。
    /// </summary>
    public static bool HasPreparedActivationPayment(
        CardModel card
    )
    {
        ArgumentNullException.ThrowIfNull(card);
        return PreparedPayments.TryGetValue(card, out var state) &&
            state.Count > 0;
    }

    public static void ClearPreparedActivationPayment(
        CardModel card
    )
    {
        ArgumentNullException.ThrowIfNull(card);
        PreparedPayments.Remove(card);
    }

    public static async Task<bool> PrepareActivationPayment(
        CardModel card
    )
    {
        if (!await CommitActivationPaymentCore(card))
        {
            return false;
        }

        PreparedPayments.GetValue(
            card,
            static _ => new PreparedPaymentState()
        ).Count++;
        return true;
    }

    /// <summary>
    /// 在 BeforeCardPlayed 中保证元气恰好支付一次。常规“催动”已预付时
    /// 消费预付标记；其他自动打出入口则在这里补付。
    /// </summary>
    internal static async Task<bool> EnsureActivationPayment(
        CardModel card
    )
    {
        ArgumentNullException.ThrowIfNull(card);

        if (PreparedPayments.TryGetValue(card, out var state) &&
            state.Count > 0)
        {
            state.Count--;
            if (state.Count == 0)
            {
                PreparedPayments.Remove(card);
            }
            return true;
        }

        return await CommitActivationPaymentCore(card);
    }

    public static void ScheduleRecovery(
        CardModel card,
        int depletedOnTurn
    )
    {
        ArgumentNullException.ThrowIfNull(card);
        if (card is not IGuWormCard)
        {
            return;
        }

        int delay = card is IGuWormCard guCard
            ? Math.Max(1, guCard.RecoveryDelayTurns)
            : 2;
        int readyTurn = Math.Max(1, depletedOnTurn) + delay;
        int existing = RecoveryReadyTurnState[card];
        if (existing <= 0 || readyTurn < existing)
        {
            RecoveryReadyTurnState[card] = readyTurn;
        }
    }

    public static bool HasRecoverySchedule(CardModel card) =>
        card is IGuWormCard && RecoveryReadyTurnState[card] > 0;

    public static bool IsRecoveryReady(
        CardModel card,
        int currentTurn
    )
    {
        if (card is not IGuWormCard)
        {
            return false;
        }

        int readyTurn = RecoveryReadyTurnState[card];
        return readyTurn > 0 && currentTurn >= readyTurn;
    }

    private static async Task<bool> CommitActivationPaymentCore(
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
