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
    private const float HandAutoHideDistance = 40f;

    // 40px 约 0.125 秒完成，避免瞬移，同时保持响应足够快。
    private const float HandAutoHideSpeed = 320f;

    // 悬停只按每张卡真正接收鼠标输入的 Hitbox 判断；四周仅保留少量容错，
    // 避免 NPlayerHand / NModExtraHand 或 NHandCardHolder 自身的布局区域过大。
    private const float HandHoverPadding = 8f;

    // Hover 命中不需要跟随渲染帧率。30Hz 已足够跟手，同时把矩形/
    // Transform 检查从 60/120/144Hz 降下来。
    private const double HandHoverScanIntervalSeconds = 1d / 30d;

    // 手牌节点树只有在抽牌/弃牌等结构变化时才需要重新递归扫描。
    // 平时直接复用缓存的 Hitbox，避免每帧 GetChildren() 遍历整棵 UI 树。
    private const double HandHitboxCacheRefreshIntervalSeconds = 0.25d;

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

    private sealed class HandHoverCacheState
    {
        public List<Control> Hitboxes { get; } = [];

        public bool HasSnapshot { get; set; }

        public double RefreshCountdown { get; set; }
    }

    private static readonly ConditionalWeakTable<
        NModExtraHand,
        ExtraHandLayoutState
    > ExtraHandLayoutStates = new();

    private static readonly ConditionalWeakTable<
        NPlayerHand,
        PrimaryHandLayoutState
    > PrimaryHandLayoutStates = new();

    private static readonly ConditionalWeakTable<
        CanvasItem,
        HandHoverCacheState
    > HandHoverCacheStates = new();

    // 普通手牌与蛊手牌共用同一个收起偏移，确保两套 UI 始终同步移动。
    private static float _handAutoHideOffsetY;
    private static double _handHoverScanAccumulator =
        HandHoverScanIntervalSeconds;
    private static bool _cachedMouseOverAnyHand;

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

        // 起手蛊牌逐张进入 ExtraHand 的过程中，牌面节点可能已经可见，
        // 但整段 CardPileCmd 入场事务尚未完成。此时开始 RitsuLib 的
        // Hand 临时迁移会与入场迁移交错，导致卡牌留在封存堆并锁住
        // 后续蛊牌选择。因此在全部起手蛊牌入场完成前统一禁用交互。
        if (GuCardPileSystem.IsOpeningEntryPending(card.Owner))
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
        if (card is not IGuWormCard)
        {
            return false;
        }

        bool isPlayablePile =
            card.Pile?.Type == GuCardPileSystem.PileType ||
            card.Pile?.Type == PileType.Hand;
        return isPlayablePile &&
            GuCardUsageRules.GetActivationEligibility(card).IsAllowed;
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
        _handHoverScanAccumulator = HandHoverScanIntervalSeconds;
        _cachedMouseOverAnyHand = false;
    }

    /// <summary>
    /// 定期清理已失效的 pending 记录。蛊牌被点击后 RitsuLib 会把它临时
    /// 移入原版 Hand 走原生目标选择（目标选择/出牌排队期间卡停留在
    /// Hand）；若玩家取消或放弃目标选择（右键、Esc、拖拽回手等），
    /// RitsuLib 会把卡移回蛊牌堆，但本模组没有对应的取消 hook，
    /// _pendingCard 会残留——此后 CanSelect 只放行该牌、拦截其余全部
    /// 蛊牌，出现“摸过/点过的蛊牌必须先打出，其他蛊虫全部禁打”的
    /// 卡死状态。pending 只应在原版 Hand 中存活；一旦卡牌被取消回蛊
    /// 手牌、异常退回封存堆，或已经进入其他牌堆，都应清理并恢复选择。
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

        if (pending.Pile?.Type != PileType.Hand)
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
        NModExtraHand extraHand,
        double deltaSeconds
    )
    {
        ArgumentNullException.ThrowIfNull(extraHand);

        if (extraHand.Definition.PileType !=
                GuCardPileSystem.PileType ||
            !GodotObject.IsInstanceValid(extraHand))
        {
            return;
        }

        double safeDeltaSeconds = Math.Max(0d, deltaSeconds);

        ExtraHandLayoutState state =
            ExtraHandLayoutStates.GetOrCreateValue(extraHand);

        Vector2 currentPosition = extraHand.Position;
        bool extraHandWasRelayout =
            !state.HasApplied ||
            !currentPosition.IsEqualApprox(state.AppliedPosition);

        if (extraHandWasRelayout)
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

        bool primaryHandWasRelayout =
            !primaryState.HasApplied ||
            !primaryCurrentPosition.IsEqualApprox(
                primaryState.AppliedPosition);

        if (primaryHandWasRelayout)
        {
            // 原版手牌首次布局、分辨率变化或游戏自身重新布局后，
            // 记录最新的“完全展开”基准位置。
            primaryState.BasePosition = primaryCurrentPosition;
        }

        Vector2 extraExpandedPosition =
            state.BasePosition + ExtraHandDownOffset;

        // 只有首次布局或外部重新布局时，先恢复到当前自动收起偏移。
        // 正常帧不再像旧实现那样先写“上一帧位置”再写“本帧位置”。
        if (extraHandWasRelayout)
        {
            Vector2 normalizedExtraPosition =
                extraExpandedPosition +
                Vector2.Down * _handAutoHideOffsetY;

            if (!extraHand.Position.IsEqualApprox(
                    normalizedExtraPosition))
            {
                extraHand.Position = normalizedExtraPosition;
            }
        }

        if (primaryHandWasRelayout)
        {
            SetCanvasItemPosition(
                primaryHand,
                primaryState.BasePosition +
                    Vector2.Down * _handAutoHideOffsetY
            );
        }

        // 卡牌 hover 检测固定为 30Hz；两套手牌的动画位移仍按实际帧率
        // 更新，所以不会把收起/展开动画降成 30FPS。
        _handHoverScanAccumulator += safeDeltaSeconds;
        if (_handHoverScanAccumulator >=
            HandHoverScanIntervalSeconds)
        {
            double hoverElapsed = _handHoverScanAccumulator;
            _handHoverScanAccumulator = 0d;

            Vector2 mousePosition =
                extraHand.GetGlobalMousePosition();

            bool isMouseOverExtraHand =
                IsMouseOverHandCards(
                    extraHand,
                    mousePosition,
                    _handAutoHideOffsetY,
                    hoverElapsed
                );

            bool isMouseOverPrimaryHand =
                IsMouseOverHandCards(
                    primaryHand,
                    mousePosition,
                    _handAutoHideOffsetY,
                    hoverElapsed
                );

            _cachedMouseOverAnyHand =
                isMouseOverExtraHand ||
                isMouseOverPrimaryHand;
        }

        float targetAutoHideOffset =
            _cachedMouseOverAnyHand ? 0f : HandAutoHideDistance;

        float maxStep =
            HandAutoHideSpeed * (float)safeDeltaSeconds;

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

        // 这些属性只在值发生变化时写入，避免每帧把同一状态重新标脏。
        if (!extraHand.Scale.IsEqualApprox(Vector2.One))
        {
            extraHand.Scale = Vector2.One;
        }

        if (extraHand.Modulate != Colors.White)
        {
            extraHand.Modulate = Colors.White;
        }

        if (extraHand.ZAsRelative)
        {
            extraHand.ZAsRelative = false;
        }

        int desiredZIndex = Math.Max(
            -4000,
            GetEffectiveZIndex(primaryHand) - ExtraHandZGap
        );

        if (extraHand.ZIndex != desiredZIndex)
        {
            extraHand.ZIndex = desiredZIndex;
        }
    }

    /// <summary>
    /// 使用缓存的 NHandCardHolder.Hitbox 进行 hover 检测。缓存低频刷新，
    /// 抽牌/弃牌造成的节点变化最多约 0.25 秒后被发现；实际 hover 命中
    /// 则以 30Hz 计算，避免逐帧递归遍历两套手牌节点树。
    /// </summary>
    private static bool IsMouseOverHandCards(
        CanvasItem handRoot,
        Vector2 mousePosition,
        float currentAutoHideOffsetY,
        double elapsedSeconds
    )
    {
        HandHoverCacheState cache =
            HandHoverCacheStates.GetOrCreateValue(handRoot);

        cache.RefreshCountdown -= elapsedSeconds;
        if (!cache.HasSnapshot ||
            cache.RefreshCountdown <= 0d)
        {
            RebuildHandHitboxCache(handRoot, cache);
        }

        foreach (Control hitbox in cache.Hitboxes)
        {
            if (!GodotObject.IsInstanceValid(hitbox))
            {
                // 下次 hover 扫描立即重建，避免继续持有已释放节点。
                cache.RefreshCountdown = 0d;
                continue;
            }

            if (!hitbox.IsVisibleInTree() ||
                hitbox.Size.X <= 0f ||
                hitbox.Size.Y <= 0f)
            {
                continue;
            }

            Rect2 hitboxRect = GetControlGlobalAabb(hitbox);

            // hitboxRect 位于当前实际展示位置。收起时向上补回已经移动
            // 的距离，让鼠标仍能从原来的卡牌位置附近“叫回”手牌；同时
            // 向下补剩余行程，避免展开/收起过程中反复抖动。
            float topExtra =
                currentAutoHideOffsetY + HandHoverPadding;

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

        return false;
    }

    private static void RebuildHandHitboxCache(
        CanvasItem handRoot,
        HandHoverCacheState cache
    )
    {
        cache.Hitboxes.Clear();
        CollectHandHitboxesRecursive(
            handRoot,
            cache.Hitboxes
        );
        cache.HasSnapshot = true;
        cache.RefreshCountdown =
            HandHitboxCacheRefreshIntervalSeconds;
    }

    private static void CollectHandHitboxesRecursive(
        Node root,
        List<Control> hitboxes
    )
    {
        foreach (Node child in root.GetChildren())
        {
            if (child is NHandCardHolder holder &&
                GodotObject.IsInstanceValid(holder) &&
                holder.IsNodeReady())
            {
                Control hitbox = holder.Hitbox;
                if (hitbox != null &&
                    GodotObject.IsInstanceValid(hitbox))
                {
                    hitboxes.Add(hitbox);
                }

                // NHandCardHolder 自身已提供最终 Hitbox，无需继续扫描
                // 它的卡面/文本/特效子树。
                continue;
            }

            CollectHandHitboxesRecursive(child, hitboxes);
        }
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
            case Control control
                when !control.Position.IsEqualApprox(position):
                control.Position = position;
                break;

            case Node2D node2D
                when !node2D.Position.IsEqualApprox(position):
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
                break;
            }

            parent = parentCanvasItem.GetParent();
        }

        return zIndex;
    }
}
