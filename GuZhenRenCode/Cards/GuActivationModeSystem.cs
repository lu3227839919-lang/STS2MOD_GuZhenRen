using System.Runtime.CompilerServices;

using Godot;

using GuZhenRen.Multiplayer;

using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;

using STS2RitsuLib.CardPiles.Nodes;

namespace GuZhenRen.Cards;

/// <summary>
/// 管理蛊手牌的直接打出流程。
///
/// 蛊虫牌位于 RitsuLib ExtraHand（蛊手牌）中，玩家直接点击蛊牌后
/// 走原生目标选择并直接打出，不再依赖“催动”牌；打出时消耗可用次数
/// 并支付元气/仙元，随后进入蛊恢复堆冷却。
/// </summary>
public static class GuActivationModeSystem
{
    private static readonly object SyncRoot = new();

    // 保留 RitsuLib 原始大卡与扇形布局，只把蛊手牌作为普通手牌
    // 后方的第二层手牌。
    private static readonly Vector2 ExtraHandDownOffset =
        new(0f, 200f);

    private const int ExtraHandZGap = 0;

    private sealed class ExtraHandLayoutState
    {
        public bool HasApplied { get; set; }

        public Vector2 BasePosition { get; set; }

        public Vector2 AppliedPosition { get; set; }
    }

    private static readonly ConditionalWeakTable<
        NModExtraHand,
        ExtraHandLayoutState
    > ExtraHandLayoutStates = new();

    // RitsuLib 在开始原生目标选择前，会把额外手牌中的卡牌临时移动到
    // 后端 PileType.Hand。记录当前被点击/结算的蛊牌，使它在这段短暂
    // 窗口中仍然通过 CardModel.CanPlay() 检查，并防止同一 pending
    // 蛊牌选择被另一张蛊牌覆盖（0.8.5 多人同步防护）。
    private static CardModel? _pendingCard;

    /// <summary>
    /// 只有本地玩家且资源可支付时，当前蛊手牌中的蛊虫才能被点击
    /// （UI 选择入口）。
    /// </summary>
    public static bool CanSelect(CardModel? card)
    {
        if (card == null ||
            !LocalContext.IsMine(card) ||
            card.Pile?.Type != GuCardPileSystem.PileType)
        {
            return false;
        }

        lock (SyncRoot)
        {
            // 一个本地 pending 记录只能对应一个即将同步的出牌动作。
            // 在它结算并清理前禁止选择另一张蛊牌，也避免多张
            // ExtraHand 卡只在发起端提前进入 Hand。
            if (_pendingCard != null &&
                !ReferenceEquals(_pendingCard, card))
            {
                return false;
            }
        }

        return CanPlay(card);
    }

    /// <summary>
    /// 判断蛊牌能否通过原生出牌管线直接打出。
    ///
    /// 蛊牌仍在自定义牌堆时是 UI 选择入口；蛊牌被 RitsuLib 移入原版
    /// Hand 后进入出牌校验阶段——该阶段由同步的 PlayCardAction 在各端
    /// 执行，不能依赖只存在于发起端（房主）的本地 pending 记录，也不
    /// 能因“不是本端玩家的牌”而拒绝，否则客户端会直接跳过蛊牌结算，
    /// 造成主客机状态分叉。
    /// </summary>
    public static bool CanPlay(CardModel? card)
    {
        if (card == null ||
            card is not IGuWormCard ||
            !GuCardUsageRules.CanActivate(card))
        {
            LogCanPlayRejection(
                card,
                card == null
                    ? "空卡"
                    : card is not IGuWormCard
                        ? "非蛊牌"
                        : "CanActivate 失败"
            );
            return false;
        }

        if (card.Pile?.Type != GuCardPileSystem.PileType &&
            card.Pile?.Type != PileType.Hand)
        {
            LogCanPlayRejection(
                card,
                $"不在可打出位置（当前牌堆 {card.Pile?.Type}）"
            );
            return false;
        }

        return true;
    }

    private static readonly object LogLock = new();

    private static readonly HashSet<string> WarnedCards = new(
        StringComparer.Ordinal
    );

    private static void LogCanPlayRejection(
        CardModel? card,
        string reason
    )
    {
        if (card == null)
        {
            return;
        }

        // 同一张卡同一原因只记一次；超过上限后整体清空，避免跨战斗
        // 无界增长或永久静默。
        string key = $"{card.Id}:{reason}";
        lock (LogLock)
        {
            if (WarnedCards.Count > 256)
            {
                WarnedCards.Clear();
            }

            if (!WarnedCards.Add(key))
            {
                return;
            }
        }

        string pendingId;
        lock (SyncRoot)
        {
            pendingId = _pendingCard?.Id.ToString() ?? "无";
        }

        Entry.Logger.Warn(
            $"[蛊牌] {card.Id} 不可打出：{reason}。" +
            $"pile={card.Pile?.Type}, pending={pendingId}, " +
            $"isMine={LocalContext.IsMine(card)}。"
        );
    }

    /// <summary>
    /// 玩家点击蛊牌开始原生目标选择时记录该蛊牌，防止同一 pending
    /// 选择被另一张蛊牌覆盖。
    /// </summary>
    internal static void MarkGuCardSelected(CardModel card)
    {
        ArgumentNullException.ThrowIfNull(card);

        lock (SyncRoot)
        {
            _pendingCard = card;
        }

        Entry.Logger.Info(
            $"[蛊牌] 已选择蛊牌 {card.Id}，等待目标确认。"
        );
    }

    private static void ClearPendingActivation(CardModel card)
    {
        lock (SyncRoot)
        {
            if (!ReferenceEquals(_pendingCard, card))
            {
                return;
            }

            _pendingCard = null;
        }
    }

    /// <summary>
    /// 蛊牌已经通过原生目标选择并正式开始结算时清理 pending 记录。
    /// </summary>
    public static void CompleteActivation(CardModel card)
    {
        ArgumentNullException.ThrowIfNull(card);

        ClearPendingActivation(card);
    }

    public static void Cancel(string reason)
    {
        bool hadPendingActivation;

        lock (SyncRoot)
        {
            hadPendingActivation = _pendingCard != null;
            _pendingCard = null;
        }

        if (!hadPendingActivation)
        {
            return;
        }

        Entry.Logger.Info($"[蛊牌] 已取消：{reason}");
    }

    public static void ResetWithoutUi()
    {
        lock (SyncRoot)
        {
            _pendingCard = null;
        }
    }

    /// <summary>
    /// 保留 RitsuLib 原始 ExtraHand 卡牌布局，同时将本模组的蛊手牌
    /// 放到普通手牌后方并整体下移。状态对象记录 RitsuLib 每次重新
    /// 布局后的基准坐标，避免逐帧重复叠加位移。
    /// </summary>
    internal static void UpdateExtraHandLayout(
        NModExtraHand extraHand
    )
    {
        ArgumentNullException.ThrowIfNull(extraHand);

        if (extraHand.Definition.PileType !=
                GuCardPileSystem.PileType ||
            !GodotObject.IsInstanceValid(extraHand))
        {
            return;
        }

        ExtraHandLayoutState state =
            ExtraHandLayoutStates.GetOrCreateValue(extraHand);

        Vector2 currentPosition = extraHand.Position;
        if (!state.HasApplied ||
            !currentPosition.IsEqualApprox(state.AppliedPosition))
        {
            // RitsuLib 初次布局或分辨率变化后给出的新基准位置。
            state.BasePosition = currentPosition;
        }

        Vector2 desiredPosition =
            state.BasePosition + ExtraHandDownOffset;

        if (!extraHand.Position.IsEqualApprox(desiredPosition))
        {
            extraHand.Position = desiredPosition;
        }

        state.AppliedPosition = desiredPosition;
        state.HasApplied = true;

        // 0.4.9 的缩放/淡化方案已撤回，恢复原始完整尺寸。
        extraHand.Scale = Vector2.One;
        extraHand.Modulate = Colors.White;

        NPlayerHand? primaryHand = NPlayerHand.Instance;
        if (primaryHand == null ||
            !GodotObject.IsInstanceValid(primaryHand))
        {
            return;
        }

        // 使用绝对 Z 值，确保蛊牌及其悬停放大仍位于第一手牌之后。
        extraHand.ZAsRelative = false;
        extraHand.ZIndex = Math.Max(
            -4000,
            GetEffectiveZIndex(primaryHand) - ExtraHandZGap
        );
    }

    private static int GetEffectiveZIndex(CanvasItem item)
    {
        int zIndex = item.ZIndex;
        if (!item.ZAsRelative)
        {
            return zIndex;
        }

        Node? parent = item.GetParent();
        while (parent is CanvasItem parentCanvasItem)
        {
            zIndex += parentCanvasItem.ZIndex;
            if (!parentCanvasItem.ZAsRelative)
            {
                break;
            }

            parent = parentCanvasItem.GetParent();
        }

        return zIndex;
    }
}
