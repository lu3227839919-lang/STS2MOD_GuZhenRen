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

    // 鼠标离开普通手牌与蛊手牌区域时，两套手牌一起下移 40px；
    // 鼠标进入任意一套手牌区域时，两套一起恢复到现有位置。
    private const float HandAutoHideDistance = 60f;

    // 40px 约 0.125 秒完成，避免瞬移，同时保持响应足够快。
    private const float HandAutoHideSpeed = 320f;

    // 悬停只按每张卡真正接收鼠标输入的 Hitbox 判断；四周仅保留少量容错，
    // 避免 NPlayerHand / NModExtraHand 或 NHandCardHolder 自身的布局区域过大。
    private const float HandHoverPadding = 8f;

    private const int ExtraHandZGap = 0;

    private sealed class ExtraHandLayoutState
    {
        public bool HasApplied { get; set; }

        public Vector2 BasePosition { get; set; }

        public Vector2 AppliedPosition { get; set; }
    }

    private sealed class PrimaryHandLayoutState
    {
        public bool HasApplied { get; set; }

        public Vector2 BasePosition { get; set; }

        public Vector2 AppliedPosition { get; set; }
    }

    private static readonly ConditionalWeakTable<
        NModExtraHand,
        ExtraHandLayoutState
    > ExtraHandLayoutStates = new();

    private static readonly ConditionalWeakTable<
        NPlayerHand,
        PrimaryHandLayoutState
    > PrimaryHandLayoutStates = new();

    // 普通手牌与蛊手牌共用同一个收起偏移，确保两套 UI 始终同步移动。
    private static float _handAutoHideOffsetY;

    // RitsuLib 在开始原生目标选择前，会把额外手牌中的卡牌临时移动到
    // 后端 PileType.Hand。记录当前被点击/结算的蛊牌，使它在这段短暂
    // 窗口中仍然通过 CardModel.CanPlay() 检查，并防止同一 pending
    // 蛊牌选择被另一张蛊牌覆盖（0.8.5 多人同步防护）。
    private static CardModel? _pendingCard;

    /// <summary>
    /// checksum 计算期间把仅在发起端提前进入 Hand 的 pending 蛊牌
    /// 静默还原到蛊牌堆。作用域结束后再恢复原版 Hand；整个过程不触发
    /// ContentsChanged，避免打断仍在进行的目标选择或刷新 ExtraHand UI。
    /// </summary>
    internal sealed class PendingChecksumScope : IDisposable
    {
        private readonly CardModel _card;

        private readonly CardPile _handPile;

        private readonly CardPile _guPile;

        private int _disposed;

        internal PendingChecksumScope(
            CardModel card,
            CardPile handPile,
            CardPile guPile
        )
        {
            _card = card;
            _handPile = handPile;
            _guPile = guPile;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            lock (SyncRoot)
            {
                // 若 checksum 期间状态已被其他结算合法推进，不覆盖新
                // 状态；正常路径中卡仍在临时蛊牌堆，且仍是当前 pending。
                if (!ReferenceEquals(_pendingCard, _card) ||
                    !ReferenceEquals(_card.Pile, _guPile))
                {
                    return;
                }

                _guPile.RemoveInternal(_card, silent: true);
                _handPile.AddInternal(_card, silent: true);
            }
        }
    }

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

    /// <summary>
    /// RitsuLib ExtraHand 会在本地目标确认前把被选蛊牌放入原版 Hand，
    /// 其他端则要等同步 PlayCardAction 执行时才补移。若队友动作此时
    /// 完成，原版 checksum 会捕获这个仅本地存在的临时状态。
    ///
    /// 本方法只在 checksum 的同步调用栈中静默调整后端牌堆；返回的
    /// 作用域负责幂等恢复。未处于该竞态时返回 null。
    /// </summary>
    internal static PendingChecksumScope?
        NormalizePendingCardForChecksum()
    {
        lock (SyncRoot)
        {
            CardModel? pending = _pendingCard;
            CardPile? handPile = pending?.Pile;
            if (pending == null ||
                handPile?.Type != PileType.Hand ||
                pending is not IGuWormCard)
            {
                return null;
            }

            CardPile guPile =
                GuCardPileSystem.PileType.GetPile(pending.Owner);
            if (ReferenceEquals(handPile, guPile))
            {
                return null;
            }

            // 不调用 InvokeContentsChanged：这里只修正 checksum 观察到的
            // 后端状态，目标选择 UI 仍持有同一 CardModel/holder。
            handPile.RemoveInternal(pending, silent: true);
            guPile.AddInternal(pending, silent: true);

            Entry.Logger.Info(
                $"[蛊牌同步] checksum 前临时还原 pending 蛊牌 " +
                $"{pending.Id} 到蛊牌堆。"
            );

            return new PendingChecksumScope(
                pending,
                handPile,
                guPile
            );
        }
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

        _handAutoHideOffsetY = 0f;
    }

    /// <summary>
    /// 每帧清理已失效的 pending 记录。蛊牌被点击后 RitsuLib 会把它临时
    /// 移入原版 Hand 走原生目标选择（目标选择/出牌排队期间卡停留在
    /// Hand）；若玩家取消或放弃目标选择（右键、Esc、拖拽回手等），
    /// RitsuLib 会把卡移回蛊牌堆，但本模组没有对应的取消 hook，
    /// _pendingCard 会残留——此后 CanSelect 只放行该牌、拦截其余全部
    /// 蛊牌，出现“摸过/点过的蛊牌必须先打出，其他蛊虫全部禁打”的
    /// 卡死状态。这里检测到 pending 卡已回到蛊牌堆即清理，恢复正常
    /// 选择；卡仍停留在 Hand 时（选择或排队进行中）保留。
    /// </summary>
    internal static void SweepStalePending()
    {
        CardModel? pending;
        lock (SyncRoot)
        {
            pending = _pendingCard;
        }

        if (pending == null)
        {
            return;
        }

        if (pending.Pile?.Type == GuCardPileSystem.PileType)
        {
            ClearPendingActivation(pending);
        }
    }

    /// <summary>
    /// 保留 RitsuLib 原始 ExtraHand 卡牌布局，同时将本模组的蛊手牌
    /// 放到普通手牌后方。鼠标位于普通手牌或蛊手牌任一区域时，两套
    /// 手牌都保持现有位置；鼠标离开两套实际卡牌区域后，两套一起
    /// 平滑下移 40px。悬停检测逐张使用 NHandCardHolder.Hitbox，
    /// 不再使用 Holder 或手牌容器的布局尺寸。状态对象分别记录原版与 RitsuLib
    /// 的布局基准，避免逐帧重复叠加位移。
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

        NPlayerHand? primaryHand = NPlayerHand.Instance;
        if (primaryHand == null ||
            !GodotObject.IsInstanceValid(primaryHand))
        {
            return;
        }

        PrimaryHandLayoutState primaryState =
            PrimaryHandLayoutStates.GetOrCreateValue(primaryHand);

        if (!TryGetCanvasItemPosition(
                primaryHand,
                out Vector2 primaryCurrentPosition))
        {
            return;
        }

        if (!primaryState.HasApplied ||
            !primaryCurrentPosition.IsEqualApprox(
                primaryState.AppliedPosition))
        {
            // 原版手牌首次布局、分辨率变化或游戏自身重新布局后，
            // 记录最新的“完全展开”基准位置。
            primaryState.BasePosition = primaryCurrentPosition;
        }

        // 先把两套手牌放到上一帧对应的展示位置，再进行 hover 判断。
        // 这样 GetLocalMousePosition() 使用的坐标系与玩家实际看到的一致。
        Vector2 extraExpandedPosition =
            state.BasePosition + ExtraHandDownOffset;
        Vector2 extraPresentationPosition =
            extraExpandedPosition +
            Vector2.Down * _handAutoHideOffsetY;

        if (!extraHand.Position.IsEqualApprox(
                extraPresentationPosition))
        {
            extraHand.Position = extraPresentationPosition;
        }

        Vector2 primaryPresentationPosition =
            primaryState.BasePosition +
            Vector2.Down * _handAutoHideOffsetY;

        SetCanvasItemPosition(
            primaryHand,
            primaryPresentationPosition
        );

        bool isMouseOverAnyHand =
            IsMouseOverHandCards(
                extraHand,
                _handAutoHideOffsetY
            ) ||
            IsMouseOverHandCards(
                primaryHand,
                _handAutoHideOffsetY
            );

        float targetAutoHideOffset =
            isMouseOverAnyHand ? 0f : HandAutoHideDistance;

        float deltaSeconds = Math.Max(
            0f,
            (float)extraHand.GetProcessDeltaTime()
        );
        float maxStep = HandAutoHideSpeed * deltaSeconds;

        _handAutoHideOffsetY = MoveTowards(
            _handAutoHideOffsetY,
            targetAutoHideOffset,
            maxStep
        );

        Vector2 extraDesiredPosition =
            extraExpandedPosition +
            Vector2.Down * _handAutoHideOffsetY;

        if (!extraHand.Position.IsEqualApprox(
                extraDesiredPosition))
        {
            extraHand.Position = extraDesiredPosition;
        }

        Vector2 primaryDesiredPosition =
            primaryState.BasePosition +
            Vector2.Down * _handAutoHideOffsetY;

        SetCanvasItemPosition(
            primaryHand,
            primaryDesiredPosition
        );

        state.AppliedPosition = extraDesiredPosition;
        state.HasApplied = true;

        primaryState.AppliedPosition = primaryDesiredPosition;
        primaryState.HasApplied = true;

        // 0.4.9 的缩放/淡化方案已撤回，恢复原始完整尺寸。
        extraHand.Scale = Vector2.One;
        extraHand.Modulate = Colors.White;

        // 使用绝对 Z 值，确保蛊牌及其悬停放大仍位于第一手牌之后。
        extraHand.ZAsRelative = false;
        extraHand.ZIndex = Math.Max(
            -4000,
            GetEffectiveZIndex(primaryHand) - ExtraHandZGap
        );
    }

    /// <summary>
    /// 逐张检测 NHandCardHolder 内真正接收鼠标输入的 Hitbox。
    ///
    /// 原版 NHandCardHolder 本身主要负责摆放、旋转和缩放，真正的鼠标
    /// 交互区域是其公开的 Hitbox（NClickableControl）。因此不能依赖
    /// Holder.Size，否则某些场景中尺寸可能为 0 或与实际卡牌点击区不符，
    /// 会导致手牌能收起却永远无法通过鼠标重新展开。
    ///
    /// 每张卡独立检测，不再把整手卡牌合并成一个大矩形，因此卡牌之间
    /// 的空白区域不会额外触发展开。
    /// </summary>
    private static bool IsMouseOverHandCards(
        CanvasItem handRoot,
        float currentAutoHideOffsetY
    )
    {
        Vector2 mousePosition =
            handRoot.GetGlobalMousePosition();

        return IsMouseOverHandCardsRecursive(
            handRoot,
            mousePosition,
            currentAutoHideOffsetY
        );
    }

    private static bool IsMouseOverHandCardsRecursive(
        Node root,
        Vector2 mousePosition,
        float currentAutoHideOffsetY
    )
    {
        foreach (Node child in root.GetChildren())
        {
            if (child is NHandCardHolder holder &&
                GodotObject.IsInstanceValid(holder) &&
                holder.IsVisibleInTree() &&
                holder.IsNodeReady())
            {
                Control hitbox = holder.Hitbox;

                if (hitbox != null &&
                    GodotObject.IsInstanceValid(hitbox) &&
                    hitbox.IsVisibleInTree() &&
                    hitbox.Size.X > 0f &&
                    hitbox.Size.Y > 0f)
                {
                    Rect2 hitboxRect =
                        GetControlGlobalAabb(hitbox);

                    // hitboxRect 位于当前实际展示位置。
                    // 收起时向上补回已经移动的距离，让鼠标仍能从原来的
                    // 卡牌位置附近“叫回”手牌；同时向下补剩余行程，
                    // 避免展开/收起过程中反复抖动。
                    float topExtra =
                        currentAutoHideOffsetY +
                        HandHoverPadding;

                    float bottomExtra =
                        (HandAutoHideDistance -
                         currentAutoHideOffsetY) +
                        HandHoverPadding;

                    Rect2 hoverRect = new(
                        hitboxRect.Position -
                            new Vector2(
                                HandHoverPadding,
                                topExtra
                            ),
                        hitboxRect.Size +
                            new Vector2(
                                HandHoverPadding * 2f,
                                topExtra + bottomExtra
                            )
                    );

                    if (hoverRect.HasPoint(mousePosition))
                    {
                        return true;
                    }
                }
            }

            if (IsMouseOverHandCardsRecursive(
                    child,
                    mousePosition,
                    currentAutoHideOffsetY))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 使用真实 Hitbox 的四个角经过 GlobalTransform 后计算 AABB，
    /// 因此卡牌扇形布局带旋转时，检测仍紧贴实际点击区域。
    /// </summary>
    private static Rect2 GetControlGlobalAabb(
        Control control
    )
    {
        Transform2D transform =
            control.GetGlobalTransform();

        Vector2 size = control.Size;

        Vector2 p0 = transform * Vector2.Zero;
        Vector2 p1 = transform * new Vector2(size.X, 0f);
        Vector2 p2 = transform * new Vector2(0f, size.Y);
        Vector2 p3 = transform * size;

        float minX = Math.Min(
            Math.Min(p0.X, p1.X),
            Math.Min(p2.X, p3.X)
        );
        float minY = Math.Min(
            Math.Min(p0.Y, p1.Y),
            Math.Min(p2.Y, p3.Y)
        );
        float maxX = Math.Max(
            Math.Max(p0.X, p1.X),
            Math.Max(p2.X, p3.X)
        );
        float maxY = Math.Max(
            Math.Max(p0.Y, p1.Y),
            Math.Max(p2.Y, p3.Y)
        );

        return new Rect2(
            new Vector2(minX, minY),
            new Vector2(maxX - minX, maxY - minY)
        );
    }

    private static bool TryGetCanvasItemPosition(
        CanvasItem item,
        out Vector2 position
    )
    {
        switch (item)
        {
            case Control control:
                position = control.Position;
                return true;

            case Node2D node2D:
                position = node2D.Position;
                return true;

            default:
                position = Vector2.Zero;
                return false;
        }
    }

    private static void SetCanvasItemPosition(
        CanvasItem item,
        Vector2 position
    )
    {
        switch (item)
        {
            case Control control:
                control.Position = position;
                break;

            case Node2D node2D:
                node2D.Position = position;
                break;
        }
    }

    private static float MoveTowards(
        float current,
        float target,
        float maxDelta
    )
    {
        if (current < target)
        {
            return Math.Min(current + maxDelta, target);
        }

        if (current > target)
        {
            return Math.Max(current - maxDelta, target);
        }

        return target;
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
                
            }

            parent = parentCanvasItem.GetParent();
        }

        return zIndex;
    }
}
