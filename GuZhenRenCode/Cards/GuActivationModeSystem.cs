using System.Reflection;
using System.Runtime.CompilerServices;

using Godot;

using HarmonyLib;

using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

using STS2RitsuLib.CardPiles.Nodes;

namespace GuZhenRen.Cards;

/// <summary>
/// 管理“催动”开启的蛊手牌选择模式。
///
/// 催动牌结算后，普通手牌暂时禁用，玩家只能从 RitsuLib ExtraHand
/// 显示的蛊手牌中选择一张。选择蛊牌后由原生打牌流程负责目标选择；
/// 目标取消时继续停留在蛊手牌模式，成功入队后恢复普通手牌。
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
    /// 只有本地玩家、当前蛊手牌中的可支付蛊虫，才能在催动模式中被点击。
    /// </summary>
    public static bool CanSelect(CardModel? card)
    {
        return card != null &&
            card.Pile?.Type == GuCardPileSystem.PileType &&
            CanPlay(card);
    }

    /// <summary>
    /// 判断蛊牌能否通过原生出牌管线。除了蛊手牌中的卡，还允许
    /// RitsuLib 正在进行目标选择、已临时移入原版 Hand 的那一张牌。
    /// </summary>
    public static bool CanPlay(CardModel? card)
    {
        if (card == null ||
            card is not IGuWormCard ||
            !LocalContext.IsMine(card) ||
            !IsActiveFor(card.Owner) ||
            !GuCardUsageRules.CanActivate(card))
        {
            return false;
        }

        if (card.Pile?.Type == GuCardPileSystem.PileType)
        {
            return true;
        }

        lock (SyncRoot)
        {
            return ReferenceEquals(_pendingCard, card) &&
                card.Pile?.Type == PileType.Hand;
        }
    }

    internal static void PrepareTargeting(CardModel card)
    {
        ArgumentNullException.ThrowIfNull(card);

        lock (SyncRoot)
        {
            if (ReferenceEquals(_activePlayer, card.Owner))
            {
                _pendingCard = card;
                Entry.Logger.Info(
                    $"[催动模式] 已选择蛊牌 {card.Id}，等待其目标确认。"
                );
            }
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

        if (!IsActiveFor(card.Owner))
        {
            return;
        }

        End(
            "[催动模式] 已选择蛊牌并确认目标，恢复普通手牌。"
        );
    }

    public static void Cancel(string reason)
    {
        if (!IsActive)
        {
            return;
        }

        End($"[催动模式] 已取消：{reason}");
    }

    public static void ResetWithoutUi()
    {
        lock (SyncRoot)
        {
            _activePlayer = null;
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

    internal static bool ShouldBlockNormalHand() => IsActive;

    internal static void CancelIfPlayerActionsDisabled()
    {
        if (IsActive &&
            CombatManager.Instance.PlayerActionsDisabled)
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
