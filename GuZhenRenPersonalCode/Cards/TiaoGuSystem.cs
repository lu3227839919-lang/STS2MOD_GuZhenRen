using System.Runtime.CompilerServices;

using Godot;

using GuZhenRen.Patches;

using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;

using STS2RitsuLib.CardPiles.Nodes;
using STS2RitsuLib.Interactions.RightClick;

namespace GuZhenRen.Cards;

/// <summary>
/// 调蛊：右键蛊手牌中的一只常规蛊，把它送入蛊存放 FIFO 队尾，
/// 随后通过统一补位流程把当前队首送入蛊手牌。
/// </summary>
internal static class TiaoGuSystem
{
    private const string GuHandTriggerMetadata =
        Entry.ModId + ":tiao_gu:gu_hand";

    private static readonly ConditionalWeakTable<Player, SemaphoreSlim>
        OperationGates = new();

    private static readonly object InputBridgeLock = new();

    private static readonly Dictionary<
        NModExtraHand,
        TiaoGuInputBridge
    > InputBridges = new(ExtraHandReferenceComparer.Instance);

    // 只保留显式同步行动注册，不让蛊牌模型实现普通手牌右键接口。
    // 原始输入桥只安装在蛊手牌 ExtraHand 节点下；普通手牌容器不会
    // 安装桥接节点，也不会进入后续的精确 Hitbox 命中测试。
    private static IDisposable? _rightClickBinding;

    private static bool _initialized;

    internal static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _rightClickBinding = ModRightClickRegistry.Register<CardModel>(
            Entry.ModId,
            "tiao_gu",
            CanStartTuneRightClick,
            HandleTuneRightClickAsync,
            priority: 200
        );
        _initialized = true;
        Entry.Logger.Info("[调蛊] 已注册蛊手牌右键绑定。");
    }

    /// <summary>
    /// 给当前蛊手牌 ExtraHand 安装原始输入桥。
    ///
    /// RitsuLib 0.5.13 已经给 ExtraHand 的 Holder/Hitbox 连接了
    /// GuiInput，但蛊手牌当前布局中的右键会在这些回调运行前被界面
    /// 输入链消费。桥接节点改从 Node._Input 捕获原始鼠标事件，再只
    /// 遍历指定 ExtraHand 子树中的 Holder，因此既不依赖 GuiInput，
    /// 也不会把普通手牌误判为调蛊对象。
    /// </summary>
    internal static void EnsureGuHandRightClickBindings(
        NModExtraHand extraHand
    )
    {
        if (!_initialized ||
            !GodotObject.IsInstanceValid(extraHand) ||
            extraHand.Definition.PileType !=
                GuCardPileSystem.PileType)
        {
            return;
        }

        lock (InputBridgeLock)
        {
            foreach (
                KeyValuePair<
                    NModExtraHand,
                    TiaoGuInputBridge
                > pair in InputBridges.ToArray())
            {
                if (GodotObject.IsInstanceValid(pair.Key) &&
                    GodotObject.IsInstanceValid(pair.Value) &&
                    pair.Value.IsInsideTree())
                {
                    continue;
                }

                if (GodotObject.IsInstanceValid(pair.Value))
                {
                    pair.Value.SetProcessInput(false);
                    pair.Value.QueueFree();
                }

                InputBridges.Remove(pair.Key);
            }

            if (InputBridges.ContainsKey(extraHand))
            {
                return;
            }

            TiaoGuInputBridge bridge = new()
            {
                Name = "TiaoGuRawInputBridge",
                ProcessMode = Node.ProcessModeEnum.Always,
            };
            bridge.Bind(extraHand);
            extraHand.AddChild(bridge);
            bridge.SetProcessInput(true);
            InputBridges.Add(extraHand, bridge);

            Entry.Logger.Info(
                "[调蛊] 已为蛊手牌安装原始右键输入桥。"
            );
        }
    }

    /// <summary>
    /// 只遍历指定 ExtraHand 的节点子树。不能使用
    /// NPlayerHand.ActiveHolders：它不包含 RitsuLib ExtraHand 卡牌。
    /// </summary>
    private static IEnumerable<NHandCardHolder>
        EnumerateGuHandHolders(Node root)
    {
        foreach (Node child in root.GetChildren())
        {
            if (child is NHandCardHolder holder)
            {
                yield return holder;
            }

            foreach (
                NHandCardHolder descendant in
                    EnumerateGuHandHolders(child))
            {
                yield return descendant;
            }
        }
    }

    /// <summary>
    /// 在 GUI 分发前处理原始右键事件。只对指定 ExtraHand 内部实际
    /// 位于鼠标下方的蛊牌 Holder 进行调蛊分发。
    /// </summary>
    internal static void OnGuHandRawInput(
        NModExtraHand extraHand,
        InputEvent inputEvent
    )
    {
        if (!_initialized ||
            inputEvent is not InputEventMouseButton mouseEvent ||
            mouseEvent.ButtonIndex != MouseButton.Right ||
            !mouseEvent.IsPressed() ||
            !GodotObject.IsInstanceValid(extraHand))
        {
            return;
        }

        if (extraHand.Definition.PileType !=
                GuCardPileSystem.PileType)
        {
            return;
        }

        NHandCardHolder? holder =
            FindTopmostGuHandHolderUnderMouse(extraHand);
        if (holder == null)
        {
            return;
        }

        CardModel? card = holder.CardModel;
        if (card == null ||
            card.Pile?.Type != GuCardPileSystem.PileType ||
            !LocalContext.IsMine(card))
        {
            return;
        }

        // 调蛊和原生出牌/选目标互斥，避免右键与正在进行的左键
        // 目标选择同时改变蛊手牌。
        NPlayerHand? hand = NPlayerHand.Instance;
        if (hand == null ||
            hand.InCardPlay ||
            NTargetManager.Instance?.IsInSelection == true)
        {
            return;
        }

        Player? player = LocalContext.GetMe(card.CombatState);
        bool dispatched =
            player != null && TryDispatchFromGuHand(card, player);

        Entry.Logger.Info(
            $"[调蛊] 原始右键已命中蛊手牌 {card.Id}；" +
            $"同步行动已提交={dispatched}。"
        );

        if (dispatched)
        {
            extraHand.GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>
    /// 精确命中当前 ExtraHand 的卡牌 Hitbox。GetLocalMousePosition 会
    /// 自动考虑卡牌的平移、缩放与旋转；同一区域有多张蛊牌重叠时，
    /// 优先选择 ZIndex 更高、其次选择绘制顺序更靠后的 Holder。
    /// </summary>
    private static NHandCardHolder?
        FindTopmostGuHandHolderUnderMouse(
            NModExtraHand extraHand
        )
    {
        NHandCardHolder? bestHolder = null;
        int bestZIndex = int.MinValue;
        int bestVisualOrder = int.MinValue;
        int visualOrder = 0;

        foreach (
            NHandCardHolder holder in
                EnumerateGuHandHolders(extraHand))
        {
            int currentVisualOrder = visualOrder++;

            if (!GodotObject.IsInstanceValid(holder) ||
                !holder.IsNodeReady() ||
                !holder.IsVisibleInTree() ||
                holder.CardModel is not CardModel card ||
                card.Pile?.Type != GuCardPileSystem.PileType)
            {
                continue;
            }

            Control hitbox = holder.Hitbox;
            if (!GodotObject.IsInstanceValid(hitbox) ||
                !hitbox.IsVisibleInTree() ||
                hitbox.Size.X <= 0f ||
                hitbox.Size.Y <= 0f)
            {
                continue;
            }

            Vector2 localMouse = hitbox.GetLocalMousePosition();
            if (localMouse.X < 0f ||
                localMouse.Y < 0f ||
                localMouse.X > hitbox.Size.X ||
                localMouse.Y > hitbox.Size.Y)
            {
                continue;
            }

            int zIndex = GetEffectiveZIndex(hitbox);
            if (bestHolder != null &&
                (zIndex < bestZIndex ||
                 (zIndex == bestZIndex &&
                  currentVisualOrder <= bestVisualOrder)))
            {
                continue;
            }

            bestHolder = holder;
            bestZIndex = zIndex;
            bestVisualOrder = currentVisualOrder;
        }

        return bestHolder;
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

    private static bool CanStartTuneRightClick(
        ModRightClickContext context
    )
    {
        // RitsuLib 0.5.13 为 ExtraHand 产生的原生触发器 Metadata 为 null；
        // 自定义 Holder/Hitbox 备用入口则使用 GuHandTriggerMetadata。
        // 两者都必须接受，否则原生 ExtraHand 右键会在这里被直接拒绝。
        if ((context.Trigger.Metadata != null &&
                context.Trigger.Metadata != GuHandTriggerMetadata) ||
            context.Trigger.Source !=
                ModRightClickSource.CombatPileCard ||
            context.Trigger.ExpectedCardPile !=
                GuCardPileSystem.PileType ||
            context.Model is not CardModel card ||
            card is not IGuWormCard)
        {
            return false;
        }

        bool canTune = CanTuneGu(card, context.Player);
        Entry.Logger.Info(
            $"[调蛊] 收到右键：{card.Id}，允许调蛊={canTune}，" +
            $"牌堆={card.Pile?.Type.ToString() ?? "无"}，" +
            $"Owner匹配={ReferenceEquals(card.Owner, context.Player)}。"
        );
        return canTune;
    }

    private static Task HandleTuneRightClickAsync(
        ModRightClickExecutionContext context
    )
    {
        if (context.Model is not CardModel card)
        {
            return Task.CompletedTask;
        }

        Entry.Logger.Info($"[调蛊] 同步右键行动已入队：{card.Id}。");
        return TuneGuAsync(card, context.Player);
    }

    /// <summary>
    /// 仅供蛊手牌 ExtraHand 的卡牌节点调用。重新标记触发来源，确保
    /// 普通手牌、牌堆浏览界面以及其他卡牌视图不能触发调蛊。
    /// </summary>
    internal static bool TryDispatchFromGuHand(
        CardModel card,
        Player player
    )
    {
        if (!CanTuneGu(card, player))
        {
            return false;
        }

        ModRightClickTrigger guHandTrigger = new(
            false,
            GuHandTriggerMetadata,
            ModRightClickSource.CombatPileCard,
            GuCardPileSystem.PileType
        );

        return ModRightClickRegistry.TryDispatch(
            new ModRightClickContext(player, card, guHandTrigger)
        );
    }

    internal static void Uninitialize()
    {
        _initialized = false;

        lock (InputBridgeLock)
        {
            foreach (
                TiaoGuInputBridge bridge in
                    InputBridges.Values)
            {
                if (!GodotObject.IsInstanceValid(bridge))
                {
                    continue;
                }

                bridge.SetProcessInput(false);
                bridge.QueueFree();
            }

            InputBridges.Clear();
        }

        _rightClickBinding?.Dispose();
        _rightClickBinding = null;
    }

    private sealed class ExtraHandReferenceComparer
        : IEqualityComparer<NModExtraHand>
    {
        internal static ExtraHandReferenceComparer Instance { get; } =
            new();

        public bool Equals(
            NModExtraHand? left,
            NModExtraHand? right
        ) => ReferenceEquals(left, right);

        public int GetHashCode(NModExtraHand extraHand) =>
            RuntimeHelpers.GetHashCode(extraHand);
    }

    internal static bool CanTuneGu(CardModel card, Player player)
    {
        if (card is not IGuWormCard ||
            player.PlayerCombatState == null ||
            NOverlayStack.Instance?.ScreenCount is > 0 ||
            !ReferenceEquals(card.Owner, player) ||
            card.Pile?.Type != GuCardPileSystem.PileType ||
            !GuCardPileSystem.PileType
                .GetPile(player)
                .Cards
                .Contains(card) ||
            GuCardPileSystem.IsOpeningEntryPending(player) ||
            GuCardPlaySyncPatch.IsCardActionExecuting(player) ||
            GuCardPileSystem.IsTemporaryCapacityBypass(card) ||
            ShaZhaoTuiYanSystem.IsMaterialSealed(card))
        {
            return false;
        }

        // 调蛊不是催动：即使元气不足或该蛊当前无法打出，只要它确实
        // 位于蛊手牌，就允许把它送到蛊抽牌队列尾并补入队首。
        return true;
    }

    internal static async Task TuneGuAsync(
        CardModel card,
        Player player
    )
    {
        SemaphoreSlim gate = OperationGates.GetValue(
            player,
            static _ => new SemaphoreSlim(1, 1)
        );
        await gate.WaitAsync();
        try
        {
            // 右键动作可能排队执行；结算前必须重新校验牌与玩家状态。
            if (!CanTuneGu(card, player))
            {
                return;
            }

            CardModel? previousQueueHead =
                GuCardPileSystem.GetStorageQueuePreview(player, 1)
                    .FirstOrDefault();

            await GuCardPileSystem.MoveCardToPileAsync(
                card,
                GuCardPileSystem.StoragePileType,
                skipVisuals: false
            );

            // MoveCardToPileAsync 已把所选蛊登记到 FIFO 队尾。
            // 统一补位会取当前队首；没有其他候选时，所选蛊会重新回手。
            await GuCardPileSystem.RefillGuHandAsync(player);

            Entry.Logger.Info(
                $"[调蛊] {card.Id} 已进入蛊抽牌队列尾；" +
                $"原队首={(previousQueueHead?.Id.ToString() ?? "无")}。"
            );
        }
        finally
        {
            gate.Release();
        }
    }
}
