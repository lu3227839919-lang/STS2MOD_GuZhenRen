using System.Runtime.CompilerServices;

using GuZhenRen.Cards.ImmortalEssence;
using GuZhenRen.Combat;

using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;

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
            "lu_gu_zhen_ren.gu_spent_activations",
            () => 0
        );

    // 0 表示尚未进入恢复；其他值是可恢复的回合编号。
    private static readonly SavedAttachedState<CardModel, int>
        RecoveryReadyTurnState = new(
            "lu_gu_zhen_ren.gu_recovery_ready_turn",
            () => 0
        );

    // 0 表示旧存档或尚未记录；其他值是恢复完成时的回合编号。
    // 用于恢复完成后按先后顺序给予蛊手牌空位（先恢复完的先上场）。
    private static readonly SavedAttachedState<CardModel, int>
        RecoveryCompletedTurnState = new(
            "lu_gu_zhen_ren.gu_recovery_completed_turn",
            () => 0
        );

    // 0 表示当前不在蛊存放队列；正数是在本场战斗内单调递增的入队序号。
    private static readonly SavedAttachedState<CardModel, int>
        StorageQueueOrderState = new(
            "lu_gu_zhen_ren.gu_storage_queue_order",
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
            // 新战斗/新一次冷却周期不继承上一次的“完成顺序”。
            // 真正冷却完成时会由 MarkRecoveryCompleted 写入本轮次。
            RecoveryCompletedTurnState[card] = 0;
            StorageQueueOrderState[card] = 0;
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

        // 被杀招封装的材料不能催动。
        if (ShaZhaoTuiYanSystem.IsMaterialSealed(card))
        {
            return false;
        }

        return card is not IGuWormCard guCard ||
            GetRemainingUses(card, guCard) > 0;
    }

    /// <summary>
    /// 把恢复就绪回合额外延后指定轮数（主动解体惩罚等）。
    /// </summary>
    public static void DelayRecoveryBy(
        CardModel card,
        int extraTurns
    )
    {
        if (extraTurns <= 0 ||
            card is not IGuWormCard ||
            ShaZhaoTuiYanSystem.IsMaterialSealed(card))
        {
            return;
        }

        int currentReady = RecoveryReadyTurnState[card];
        if (currentReady > 0)
        {
            RecoveryReadyTurnState[card] =
                currentReady + extraTurns;
        }
    }

    public static void AccelerateRecoveryBy(
        CardModel card,
        int turns,
        int currentTurn
    )
    {
        if (turns <= 0 ||
            card is not IGuWormCard ||
            ShaZhaoTuiYanSystem.IsMaterialSealed(card))
        {
            return;
        }

        int currentReady = RecoveryReadyTurnState[card];
        if (currentReady > 0)
        {
            RecoveryReadyTurnState[card] = Math.Max(
                Math.Max(1, currentTurn) + 1,
                currentReady - turns
            );
        }
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
        // 艳丽围巾在第 5 张牌前会进入 Active 状态。原版只把原生
        // 能量费用降为 0；蛊牌的元气属于 RitsuLib 次级资源，因此
        // 这里同步把本次催动视为无需元气，避免在出牌前被资源检查拦截。
        if (IsBrilliantScarfFreePlay(card))
        {
            return true;
        }

        int cost = Math.Max(0, guCard.YuanQiCost);
        return cost == 0 ||
            SecondaryResourceCmd.Get(
                card.Owner,
                YuanQiSystem.ResourceId
            ) >= cost;
    }

    /// <summary>
    /// 艳丽围巾已准备令下一张（第 5 张）牌免费时，蛊牌的元气费用
    /// 也应同步免费。蛊牌仍由原版 BrilliantScarf.AfterCardPlayed
    /// 正常计入出牌次数，本方法不修改计数。
    /// </summary>
    internal static bool IsBrilliantScarfFreePlay(CardModel card)
    {
        ArgumentNullException.ThrowIfNull(card);

        if (!CombatManager.Instance.IsInProgress || card.IsCanonical)
        {
            return false;
        }

        return card.Owner.Relics.Any(relic =>
            relic is BrilliantScarf &&
            relic.Status == RelicStatus.Active
        );
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
        if (card is not IGuWormCard ||
            ShaZhaoTuiYanSystem.IsMaterialSealed(card))
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
            if (existing <= 0)
            {
                RecoveryCompletedTurnState[card] = 0;
            }
        }
    }

    /// <summary>
    /// 从零强制恢复：无视此前已有的恢复进度，重新按完整恢复周期
    /// 安排恢复就绪回合（可额外延后指定轮数）。
    /// </summary>
    public static void ResetRecovery(
        CardModel card,
        int depletedOnTurn,
        int extraTurns = 0
    )
    {
        ArgumentNullException.ThrowIfNull(card);
        if (card is not IGuWormCard guCard ||
            ShaZhaoTuiYanSystem.IsMaterialSealed(card))
        {
            return;
        }

        int delay = Math.Max(1, guCard.RecoveryDelayTurns) + extraTurns;
        RecoveryReadyTurnState[card] =
            Math.Max(1, depletedOnTurn) + delay;
        RecoveryCompletedTurnState[card] = 0;
    }

    public static bool HasRecoverySchedule(CardModel card) =>
        card is IGuWormCard &&
        !ShaZhaoTuiYanSystem.IsMaterialSealed(card) &&
        RecoveryReadyTurnState[card] > 0;

    /// <summary>取得当前计划恢复回合；未安排恢复时返回0。</summary>
    public static int GetRecoveryReadyTurn(CardModel card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return card is IGuWormCard &&
            !ShaZhaoTuiYanSystem.IsMaterialSealed(card)
                ? RecoveryReadyTurnState[card]
                : 0;
    }

    /// <summary>
    /// 将已安排的恢复回合向前推进。允许推进到当前回合，供宙道
    /// 【岁满】在战斗中立即完成最后一回合恢复。
    /// </summary>
    public static int ReduceRecoveryReadyTurn(
        CardModel card,
        int turns,
        int currentTurn
    )
    {
        ArgumentNullException.ThrowIfNull(card);
        if (turns <= 0 ||
            card is not IGuWormCard ||
            ShaZhaoTuiYanSystem.IsMaterialSealed(card))
        {
            return GetRecoveryReadyTurn(card);
        }

        int currentReady = RecoveryReadyTurnState[card];
        if (currentReady <= 0)
        {
            return 0;
        }

        int minimumReady = Math.Max(1, currentTurn);
        int nextReady = Math.Max(minimumReady, currentReady - turns);
        RecoveryReadyTurnState[card] = nextReady;
        return nextReady;
    }

    public static bool IsRecoveryReady(
        CardModel card,
        int currentTurn
    )
    {
        if (card is not IGuWormCard ||
            ShaZhaoTuiYanSystem.IsMaterialSealed(card))
        {
            return false;
        }

        int readyTurn = RecoveryReadyTurnState[card];
        return readyTurn > 0 && currentTurn >= readyTurn;
    }

    /// <summary>
    /// 记录蛊虫恢复完成的回合，用于恢复完成后按先后顺序给予手牌空位。
    /// 旧存档中无记录的牌视为最早恢复完成（按 0 排序）。
    /// </summary>
    public static void MarkRecoveryCompleted(
        CardModel card,
        int turnNumber
    )
    {
        ArgumentNullException.ThrowIfNull(card);

        if (card is IGuWormCard)
        {
            RecoveryCompletedTurnState[card] = turnNumber;
        }
    }

    /// <summary>
    /// 取得蛊虫恢复完成时的回合编号；未记录或非蛊虫返回 0。
    /// </summary>
    public static int GetRecoveryCompletedTurn(CardModel card)
    {
        ArgumentNullException.ThrowIfNull(card);

        return card is IGuWormCard
            ? RecoveryCompletedTurnState[card]
            : 0;
    }

    public static int GetStorageQueueOrder(CardModel card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return card is IGuWormCard
            ? Math.Max(0, StorageQueueOrderState[card])
            : 0;
    }

    internal static void SetStorageQueueOrder(CardModel card, int order)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (card is IGuWormCard)
        {
            StorageQueueOrderState[card] = Math.Max(0, order);
        }
    }

    internal static void ClearStorageQueueOrder(CardModel card) =>
        SetStorageQueueOrder(card, 0);

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
