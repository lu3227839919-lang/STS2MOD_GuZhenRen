using System.Reflection;
using System.Runtime.CompilerServices;

using Godot;

using HarmonyLib;

using GuZhenRen.Cards.Basic;
using GuZhenRen.Multiplayer;

using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

using STS2RitsuLib.CardPiles.Nodes;

namespace GuZhenRen.Cards;

/// <summary>
/// 管理蛊手牌的直接催动流程。
///
/// 玩家手牌中存在一张可支付费用的“催动”时，可以直接从 RitsuLib
/// ExtraHand 选择蛊牌并使用原生目标选择。目标确认后，系统自动打出
/// 预留的“催动”；没有可用催动时，蛊牌保持不可用。
/// </summary>
public static class GuActivationModeSystem
{
    private static readonly MethodInfo AnimDisableMethod =
        AccessTools.DeclaredMethod(
            typeof(NPlayerHand),
            "AnimDisable"
        ) ?? throw new MissingMethodException(
            typeof(NPlayerHand).FullName,
            "AnimDisable()"
        );

    private static readonly MethodInfo AnimEnableMethod =
        AccessTools.DeclaredMethod(
            typeof(NPlayerHand),
            "AnimEnable"
        ) ?? throw new MissingMethodException(
            typeof(NPlayerHand).FullName,
            "AnimEnable()"
        );

    private static readonly object SyncRoot = new();

    // 保留 RitsuLib 原始大卡与扇形布局，只把蛊手牌作为普通手牌
    // 后方的第二层手牌。普通手牌禁用时会向下移动 100 像素，正好
    // 露出后方蛊手牌，完成催动后再由普通手牌遮住其下半部分。
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

    private static Player? _activePlayer;

    // RitsuLib 在开始原生目标选择前，会把额外手牌中的卡牌临时移动到
    // 后端 PileType.Hand。记录当前被点击的蛊牌，使它在这段短暂窗口中
    // 仍然通过 CardModel.CanPlay() 检查。
    private static CardModel? _pendingCard;

    // 直接点击蛊牌时，预留手牌中将被自动打出的“催动”。
    // 预留只跨越原生目标选择窗口，不写入存档。
    private static ChuiDong? _pendingActivator;

    // “催动”改为只能由蛊牌流程自动打出。这个标记仅在
    // CardCmd.AutoPlay 的同步调用链中开放其 IsPlayable。
    private static CardModel? _autoPlayingActivator;

    public static bool IsActive
    {
        get
        {
            lock (SyncRoot)
            {
                return _activePlayer != null;
            }
        }
    }

    public static bool IsActiveFor(Player? player)
    {
        lock (SyncRoot)
        {
            return player != null &&
                ReferenceEquals(_activePlayer, player);
        }
    }

    /// <summary>
    /// 只有本地玩家、资源可支付且手牌中存在可用催动时，
    /// 当前蛊手牌中的蛊虫才能被点击（UI 选择入口）。
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
            // 在它结算并清理前禁止选择另一张蛊牌，避免覆盖预留催动，
            // 也避免多张 ExtraHand 卡只在发起端提前进入 Hand。
            if (_pendingCard != null &&
                !ReferenceEquals(_pendingCard, card))
            {
                return false;
            }
        }

        return CanPlay(card);
    }

    /// <summary>
    /// 判断蛊牌能否通过原生出牌管线。
    ///
    /// 蛊牌仍在自定义牌堆时是 UI 选择入口（仅本地玩家、手牌有可用催动
    /// 才可选）；蛊牌被 RitsuLib 移入原版 Hand 后进入出牌校验阶段——
    /// 该阶段由同步的 PlayCardAction 在各端执行，不能依赖只存在于发起端
    /// （房主）的本地 pending 记录，也不能因“不是本端玩家的牌”而拒绝，
    /// 否则客户端会直接跳过蛊牌结算，造成主客机状态分叉。
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

        Player owner = card.Owner;

        // 蛊手牌中的牌只有在普通手牌里存在一张当前可支付费用的
        // “催动”时才显示为可用；玩家直接点击蛊牌，不再先进入模式。
        // 本方法同时被 UI（CanSelect 已单独校验 IsMine）与各端同步的
        // 出牌动作执行使用，后者不能因“不是本端玩家的牌”拒绝。
        if (card.Pile?.Type == GuCardPileSystem.PileType)
        {
            bool available = FindAvailableActivator(owner) != null;
            if (!available)
            {
                LogCanPlayRejection(card, "蛊牌堆中无可用催动");
            }

            return available;
        }

        // RitsuLib 开始原生目标选择时会把被点击的蛊牌临时移入 Hand。
        // 这段窗口同时服务房主（本地 pending）与客户端（同步出牌动作，
        // 没有 pending、且卡牌不属于本端玩家），统一按“手牌存在可用
        // 催动”放行。
        if (card.Pile?.Type == PileType.Hand)
        {
            bool available = FindAvailableActivator(owner) != null;
            if (!available)
            {
                LogCanPlayRejection(card, "Hand 中无可用催动");
            }

            return available;
        }

        LogCanPlayRejection(
            card,
            $"不在可打出位置（当前牌堆 {card.Pile?.Type}）"
        );
        return false;
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
            $"[蛊牌催动] {card.Id} 不可打出：{reason}。" +
            $"pile={card.Pile?.Type}, pending={pendingId}, " +
            $"isMine={LocalContext.IsMine(card)}。"
        );
    }

    internal static void PrepareTargeting(CardModel card)
    {
        ArgumentNullException.ThrowIfNull(card);

        ChuiDong? activator = FindAvailableActivator(card.Owner);

        lock (SyncRoot)
        {
            _pendingCard = card;
            _pendingActivator = activator;
        }

        Entry.Logger.Info(
            $"[蛊牌催动] 已选择蛊牌 {card.Id}，等待目标确认；" +
            $"预留催动={activator?.Id.ToString() ?? "无"}。"
        );
    }

    /// <summary>
    /// 蛊牌正式开始结算时，自动支付并打出目标选择前预留的“催动”。
    ///
    /// 发起端（房主）使用本地预留的催动；客户端执行同一同步出牌动作时
    /// 没有 pending 记录，直接查找手牌中可用的催动并打出，保证两端对
    /// 催动与能量的消耗一致。脚本 AutoPlay（isAutoPlay=true）不消费
    /// 催动牌。
    /// </summary>
    internal static async Task<bool>
        TryAutoPlayReservedActivatorAsync(
            CardModel guCard,
            bool isAutoPlay
        )
    {
        ArgumentNullException.ThrowIfNull(guCard);

        ChuiDong? activator;
        bool hasPending;
        lock (SyncRoot)
        {
            hasPending = ReferenceEquals(_pendingCard, guCard);
            activator = hasPending ? _pendingActivator : null;
        }

        Player owner = guCard.Owner;
        if (!hasPending)
        {
            if (isAutoPlay)
            {
                // 没有经过 ExtraHand 直接选择的脚本入口沿用原有行为：
                // 不自动打出催动。
                return true;
            }

            // 客户端执行同步的蛊牌出牌动作：本地没有 pending 记录，
            // 直接查找手牌中可用的催动，保证与发起端消耗一致。
            activator = FindAvailableActivator(owner);
            if (activator == null)
            {
                Entry.Logger.Warn(
                    $"[蛊牌催动] {guCard.Id} 出牌时本端未找到可用催动；" +
                    $"pile={guCard.Pile?.Type}, isAutoPlay={isAutoPlay}。"
                );
                return false;
            }
        }
        else if (!IsActivatorAvailable(activator, owner))
        {
            // 目标选择期间预留催动失效（被弃置/打出等），回退到当前
            // 手牌中可用的催动，避免对已离手的卡牌执行 AutoPlay。
            activator = FindAvailableActivator(owner);
            if (activator == null)
            {
                ClearPendingActivation(guCard);
                return false;
            }
        }

        if (activator == null ||
            owner.PlayerCombatState == null)
        {
            ClearPendingActivation(guCard);
            return false;
        }

        int energyCost = GetActivatorEnergyCost(activator);
        if (owner.PlayerCombatState.Energy < energyCost)
        {
            ClearPendingActivation(guCard);
            return false;
        }

        if (energyCost > 0)
        {
            await activator.SpendEnergy(energyCost);
        }

        lock (SyncRoot)
        {
            _autoPlayingActivator = activator;
        }

        try
        {
            await CardCmd.AutoPlay(
                new BlockingPlayerChoiceContext(),
                activator,
                owner.Creature,
                skipXCapture: false
            );

            Entry.Logger.Info(
                $"[蛊牌催动] 打出 {guCard.Id} 时已自动打出 " +
                $"{activator.Id}，能量费用={energyCost}，" +
                $"pending={hasPending}。"
            );
            return true;
        }
        finally
        {
            lock (SyncRoot)
            {
                if (ReferenceEquals(
                        _autoPlayingActivator,
                        activator
                    ))
                {
                    _autoPlayingActivator = null;
                }
            }

            ClearPendingActivation(guCard);
        }
    }

    internal static bool IsAutoPlayingActivator(CardModel? card)
    {
        lock (SyncRoot)
        {
            return card != null &&
                ReferenceEquals(_autoPlayingActivator, card);
        }
    }

    /// <summary>
    /// ShouldPlay 阶段再次确认目标选择前预留的催动仍然可用。
    /// 脚本 AutoPlay 没有 pending 记录，继续沿用原有入口。
    /// </summary>
    internal static bool CanResolvePendingActivation(CardModel card)
    {
        ArgumentNullException.ThrowIfNull(card);

        ChuiDong? activator;
        lock (SyncRoot)
        {
            if (!ReferenceEquals(_pendingCard, card))
            {
                return true;
            }

            activator = _pendingActivator;
        }

        return IsActivatorAvailable(activator, card.Owner) ||
            FindAvailableActivator(card.Owner) != null;
    }

    private static ChuiDong? FindAvailableActivator(Player owner)
    {
        return owner.PlayerCombatState?.Hand.Cards
            .OfType<ChuiDong>()
            .Where(card => IsActivatorAvailable(card, owner))
            .OrderBy(GetActivatorEnergyCost)
            .ThenBy(GuZhenRenDeterminism.GetCardNetworkId)
            .FirstOrDefault();
    }

    private static bool IsActivatorAvailable(
        ChuiDong? activator,
        Player owner
    )
    {
        return activator != null &&
            !activator.IsCanonical &&
            ReferenceEquals(activator.Owner, owner) &&
            activator.Pile?.Type == PileType.Hand &&
            owner.PlayerCombatState != null &&
            owner.PlayerCombatState.Energy >=
                GetActivatorEnergyCost(activator);
    }

    private static int GetActivatorEnergyCost(
        ChuiDong activator
    )
    {
        return activator.EnergyCost.CostsX
            ? 0
            : Math.Max(
                0,
                activator.EnergyCost.GetWithModifiers(
                    CostModifiers.All
                )
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
            _pendingActivator = null;
        }
    }

    public static bool Begin(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);

        if (!LocalContext.IsMe(player) ||
            player.PlayerCombatState == null ||
            !GuCardPileSystem.PileType
                .GetPile(player)
                .Cards
                .Any(CanBeginWith))
        {
            return false;
        }

        lock (SyncRoot)
        {
            if (_activePlayer != null)
            {
                return false;
            }

            _activePlayer = player;
            _pendingCard = null;
            _pendingActivator = null;
        }

        Entry.Logger.Info(
            "[催动模式] 蛊手牌已激活；普通手牌暂时禁用。"
        );

        // 延迟到“催动”本身完成离手后再更新手牌动画和控制器焦点，
        // 避免与当前卡牌的回手/弃置动画争用 holder。
        Callable.From(ApplyActiveUi).CallDeferred();
        return true;
    }

    /// <summary>
    /// 蛊牌已经通过原生目标选择并正式开始结算时结束模式。
    /// </summary>
    public static void CompleteActivation(CardModel card)
    {
        ArgumentNullException.ThrowIfNull(card);

        ClearPendingActivation(card);

        if (IsActiveFor(card.Owner))
        {
            End(
                "[催动模式] 已选择蛊牌并确认目标，恢复普通手牌。"
            );
        }
    }

    public static void Cancel(string reason)
    {
        bool hadActiveMode;
        bool hadPendingActivation;

        lock (SyncRoot)
        {
            hadActiveMode = _activePlayer != null;
            hadPendingActivation = _pendingCard != null;

            _activePlayer = null;
            _pendingCard = null;
            _pendingActivator = null;
            _autoPlayingActivator = null;
        }

        if (!hadActiveMode && !hadPendingActivation)
        {
            return;
        }

        Entry.Logger.Info($"[蛊牌催动] 已取消：{reason}");

        if (hadActiveMode)
        {
            RestoreNormalHandUi();
        }
    }

    public static void ResetWithoutUi()
    {
        lock (SyncRoot)
        {
            _activePlayer = null;
            _pendingCard = null;
            _pendingActivator = null;
            _autoPlayingActivator = null;
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

    internal static bool ShouldBlockNormalHand() => IsActive;

    internal static void CancelIfPlayerActionsDisabled()
    {
        if (CombatManager.Instance.PlayerActionsDisabled)
        {
            Cancel("玩家操作阶段已经结束。");
        }
    }

    private static bool CanBeginWith(CardModel card) =>
        card is IGuWormCard && GuCardUsageRules.CanActivate(card);

    private static void End(string logMessage)
    {
        lock (SyncRoot)
        {
            _activePlayer = null;
            _pendingCard = null;
            _pendingActivator = null;
            _autoPlayingActivator = null;
        }

        Entry.Logger.Info(logMessage);
        RestoreNormalHandUi();
    }

    private static void ApplyActiveUi()
    {
        if (!IsActive)
        {
            return;
        }

        NPlayerHand? hand = NPlayerHand.Instance;
        if (hand != null && GodotObject.IsInstanceValid(hand))
        {
            AnimDisableMethod.Invoke(hand, null);
        }

        RefreshExtraHandLayout();
        FocusFirstSelectableGuCard();
    }

    private static void RestoreNormalHandUi()
    {
        NPlayerHand? hand = NPlayerHand.Instance;
        if (hand == null || !GodotObject.IsInstanceValid(hand))
        {
            return;
        }

        // 敌方回合、战斗结束或动作锁定期间，由原版状态机决定何时重新
        // 启用手牌；此处强行 AnimEnable 会造成错误的可操作外观。
        if (CombatManager.Instance.PlayerActionsDisabled ||
            CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        AnimEnableMethod.Invoke(hand, null);
        RefreshExtraHandLayout();
    }

    private static void RefreshExtraHandLayout()
    {
        NCombatRoom? room = NCombatRoom.Instance;
        if (room == null || !GodotObject.IsInstanceValid(room))
        {
            return;
        }

        foreach (Node node in EnumerateDescendants(room))
        {
            if (node is NModExtraHand extraHand &&
                extraHand.Definition.PileType ==
                    GuCardPileSystem.PileType)
            {
                UpdateExtraHandLayout(extraHand);
            }
        }
    }

    private static void FocusFirstSelectableGuCard()
    {
        NCombatRoom? room = NCombatRoom.Instance;
        if (room == null || !GodotObject.IsInstanceValid(room))
        {
            return;
        }

        foreach (Node node in EnumerateDescendants(room))
        {
            if (node is not NModExtraHand extraHand ||
                extraHand.Definition.PileType != GuCardPileSystem.PileType)
            {
                continue;
            }

            CardModel? first = GuCardPileSystem.PileType
                .GetPile(_activePlayer!)
                .Cards
                .FirstOrDefault(CanSelect);

            NHandCardHolder? holder =
                first == null ? null : extraHand.GetHolder(first);

            if (holder != null && GodotObject.IsInstanceValid(holder))
            {
                holder.GrabFocus();
            }

            return;
        }
    }

    private static IEnumerable<Node> EnumerateDescendants(Node root)
    {
        Stack<Node> pending = new();

        foreach (Node child in root.GetChildren())
        {
            pending.Push(child);
        }

        while (pending.Count > 0)
        {
            Node current = pending.Pop();
            yield return current;

            foreach (Node child in current.GetChildren())
            {
                pending.Push(child);
            }
        }
    }
}
