using System.Runtime.CompilerServices;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using Godot;

using STS2RitsuLib.CardPiles;

namespace GuZhenRen.Cards;

/// <summary>
/// 蛊虫专用战斗区域。可用蛊虫显示在 RitsuLib ExtraHand 蛊手牌中，
/// 耗尽的蛊虫进入蛊恢复堆；它们不会长期进入原版普通手牌。
/// </summary>
public static class GuCardPileSystem
{
    public const string LocalId = "gu_cards";

    public const string RecoveryLocalId = "gu_discard";

    /// <summary>Fully-qualified RitsuLib card-pile ID used by localization.</summary>
    public const string PileId = "GU_ZHEN_REN_CARDPILE_GU_CARDS";

    /// <summary>Fully-qualified RitsuLib ID for the recovering Gu pile.</summary>
    public const string RecoveryPileId =
        "GU_ZHEN_REN_CARDPILE_GU_DISCARD";

    public const string DiscardPileId = RecoveryPileId;

    /// <summary>The runtime pile type assigned by RitsuLib.</summary>
    public static PileType PileType { get; private set; }

    /// <summary>The runtime pile type used by Gu cards with no uses left.</summary>
    public static PileType RecoveryPileType { get; private set; }

    public static PileType DiscardPileType => RecoveryPileType;

    private static readonly object SyncRoot = new();

    private sealed class OpeningEntryState
    {
        public CardModel[] Cards { get; set; } = [];

        public bool Started { get; set; }

        public bool Completed { get; set; }

        public Task? AnimationTask { get; set; }
    }

    private static readonly ConditionalWeakTable<
        Player,
        OpeningEntryState
    > OpeningEntryStates = new();

    private static bool _initialized;

    public static void Initialize()
    {
        lock (SyncRoot)
        {
            if (_initialized)
            {
                return;
            }

            ModCardPileRegistry registry =
                ModCardPileRegistry.For(Entry.ModId);

            // 蛊存放堆以 RitsuLib ExtraHand 形式常驻显示。卡牌节点始终可见，
            // 但只有“催动模式”开启时才允许开始原生出牌/目标选择流程。
            ModCardPileDefinition definition =
                registry.RegisterOwned(
                    LocalId,
                    new ModCardPileSpec
                    {
                        Scope = ModCardPileScope.CombatOnly,
                        Style = ModCardPileUiStyle.ExtraHand,
                        CardShouldBeVisible = true,
                        ExtraHand = new ModCardPileExtraHandSpec
                        {
                            AllowCardPlay = true,
                            ShowPlayableGlow = true,
                        },
                    }
                );

            // 恢复堆仍放在原版弃牌堆左侧。
            ModCardPileDefinition recoveryDefinition =
                registry.RegisterOwned(
                    RecoveryLocalId,
                    new ModCardPileSpec
                    {
                        Scope = ModCardPileScope.CombatOnly,
                        Style = ModCardPileUiStyle.BottomLeft,
                        IconPath =
                            "res://GuZhenRen/images/ui/QiPaiDui.png",

                        Anchor = new ModCardPileAnchor(
                            ModCardPileAnchorKind.BottomLeftSecondary,
                            new Vector2(-200f, 0f)
                        ),
                    }
                );

            PileType = definition.PileType;
            RecoveryPileType = recoveryDefinition.PileType;
            _initialized = true;
        }
    }


    public static void Uninitialize()
    {
        lock (SyncRoot)
        {
            // RitsuLib 的牌堆注册是进程级且不可撤销的。初始化回滚时保留
            // 标记，避免后续重试以相同 ID 重复注册并导致模组无法加载。
        }
    }

    /// <summary>
    /// Adds an already-created Gu card to this combat-only pile.
    /// </summary>
    public static async Task<bool> AddGuCardToCombat(
        CardModel card,
        Player owner
    )
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(owner);

        EnsureInitialized();
        GuCardUsageRules.ResetUses(card);

        CardPileAddResult result =
            await CardPileCmd.AddGeneratedCardToCombat(
                card,
                PileType,
                owner
            );

        return result.success;
    }

    /// <summary>
    /// 战斗初始化时先把蛊牌暂存到恢复堆。第一轮原版抽牌开始时，
    /// <see cref="BeginOpeningGuEntry"/> 会把它们以 RitsuLib 牌堆飞行动画
    /// 送入 ExtraHand，使蛊牌入场与普通起手抽牌并行播放。
    /// </summary>
    internal static void InitializeGuCardsForCombat(Player owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        EnsureInitialized();

        CardPile recoveryPile = RecoveryPileType.GetPile(owner);
        CardPile[] combatPiles =
        [
            PileType.Draw.GetPile(owner),
            PileType.Discard.GetPile(owner),
            PileType.Hand.GetPile(owner),
            PileType.GetPile(owner),
            recoveryPile,
        ];

        CardModel[] guCards = combatPiles
            .SelectMany(static pile => pile.Cards)
            .Where(static card => card is IGuWormCard)
            .Distinct()
            .ToArray();

        HashSet<CardPile> changedPiles = [];
        foreach (CardModel card in guCards)
        {
            GuCardUsageRules.ResetUses(card);

            CardPile? sourcePile = card.Pile;
            if (sourcePile != null &&
                !ReferenceEquals(sourcePile, recoveryPile))
            {
                sourcePile.RemoveInternal(card, silent: true);
                changedPiles.Add(sourcePile);
                recoveryPile.AddInternal(card, silent: true);
                changedPiles.Add(recoveryPile);
            }
        }

        foreach (CardPile changedPile in changedPiles)
        {
            changedPile.InvokeContentsChanged();
        }

        OpeningEntryStates.Remove(owner);
        OpeningEntryState state = OpeningEntryStates.GetValue(
            owner,
            static _ => new OpeningEntryState()
        );
        state.Cards = guCards;
        state.Started = false;
        state.Completed = guCards.Length == 0;
        state.AnimationTask = null;

        if (guCards.Length > 0)
        {
            Entry.Logger.Info(
                $"[蛊牌入场] 已将 {guCards.Length} 张蛊牌暂存至恢复堆，等待首轮抽牌。"
            );
        }
    }

    /// <summary>
    /// 在首轮 <c>CardPileCmd.DrawInternal</c> 开始前启动蛊牌入场。
    /// 返回的任务会与原版抽牌任务并行运行，并由 Harmony 后置包装共同等待。
    /// </summary>
    internal static Task? BeginOpeningGuEntry(
        Player owner,
        bool fromHandDraw
    )
    {
        ArgumentNullException.ThrowIfNull(owner);

        if (!fromHandDraw ||
            owner.PlayerCombatState?.TurnNumber != 1 ||
            !OpeningEntryStates.TryGetValue(owner, out var state))
        {
            return null;
        }

        lock (state)
        {
            if (state.Started || state.Completed)
            {
                return null;
            }

            state.Started = true;
            state.AnimationTask = RunOpeningGuEntryAsync(owner, state);
            return state.AnimationTask;
        }
    }

    private static async Task RunOpeningGuEntryAsync(
        Player owner,
        OpeningEntryState state
    )
    {
        CardPile recoveryPile = RecoveryPileType.GetPile(owner);
        CardModel[] cards = state.Cards
            .Where(card =>
                card is IGuWormCard &&
                ReferenceEquals(card.Pile, recoveryPile)
            )
            .ToArray();

        try
        {
            if (cards.Length == 0)
            {
                return;
            }

            Entry.Logger.Info(
                $"[蛊牌入场] 与首轮抽牌同步播放 {cards.Length} 张蛊牌的恢复堆入场动画。"
            );

            await CardPileCmd.Add(
                cards,
                PileType,
                CardPilePosition.Bottom,
                clonedBy: null,
                skipVisuals: false
            );
        }
        catch (Exception exception)
        {
            Entry.Logger.Error(
                $"[蛊牌入场] 动画执行失败，已回退为无动画入场：{exception}"
            );

            CardPile guPile = PileType.GetPile(owner);
            foreach (CardModel card in cards)
            {
                if (!ReferenceEquals(card.Pile, recoveryPile))
                {
                    continue;
                }

                recoveryPile.RemoveInternal(card, silent: true);
                guPile.AddInternal(card, silent: true);
            }

            recoveryPile.InvokeContentsChanged();
            guPile.InvokeContentsChanged();
        }
        finally
        {
            lock (state)
            {
                state.Completed = true;
                state.Cards = [];
                state.AnimationTask = null;
            }
        }
    }

    private static bool IsOpeningEntryPending(
        Player owner,
        CardModel card
    )
    {
        if (!OpeningEntryStates.TryGetValue(owner, out var state) ||
            state.Completed)
        {
            return false;
        }

        lock (state)
        {
            return !state.Completed && state.Cards.Contains(card);
        }
    }

    internal static void MoveStrayGuCardsToVillage(Player owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        EnsureInitialized();

        CardPile guPile = PileType.GetPile(owner);
        CardPile recoveryPile = RecoveryPileType.GetPile(owner);

        foreach (CardPile sourcePile in new[]
        {
            PileType.Draw.GetPile(owner),
            PileType.Discard.GetPile(owner),
            PileType.Hand.GetPile(owner),
        })
        {
            CardModel[] guCards = sourcePile.Cards
                .Where(card => card is IGuWormCard)
                .ToArray();

            if (guCards.Length == 0)
            {
                continue;
            }

            foreach (CardModel card in guCards)
            {
                sourcePile.RemoveInternal(card, silent: true);

                if (GuCardUsageRules.CanUse(card))
                {
                    guPile.AddInternal(card, silent: true);
                }
                else
                {
                    if (!GuCardUsageRules.HasRecoverySchedule(card))
                    {
                        int currentTurn =
                            owner.PlayerCombatState?.TurnNumber ?? 1;
                        GuCardUsageRules.ScheduleRecovery(
                            card,
                            currentTurn
                        );
                    }
                    recoveryPile.AddInternal(card, silent: true);
                }
            }

            sourcePile.InvokeContentsChanged();
        }

        guPile.InvokeContentsChanged();
        recoveryPile.InvokeContentsChanged();
    }

    /// <summary>
    /// Chooses the result pile before a Gu card begins resolving.  The current
    /// play is not yet present in combat history at this point, so it is added
    /// explicitly when calculating the remaining uses.
    /// </summary>
    public static PileType GetResultPileAfterActivation(CardModel card)
    {
        ArgumentNullException.ThrowIfNull(card);

        EnsureInitialized();

        if (card is not IGuWormCard)
        {
            return PileType;
        }

        int remainingUses = Math.Max(
            0,
            GuCardUsageRules.GetRemainingUses(card) - 1
        );

        if (remainingUses == 0)
        {
            int currentTurn =
                card.Owner.PlayerCombatState?.TurnNumber ?? 1;
            GuCardUsageRules.ScheduleRecovery(card, currentTurn);
            return RecoveryPileType;
        }

        return PileType;
    }

    /// <summary>
    /// Moves every Gu card that has no uses left out of the active Gu pile.
    /// CardPileCmd supplies the same pile-flight animation used by the base game.
    /// </summary>
    public static async Task MoveDepletedGuCardsToRecoveryAsync(
        Player owner
    )
    {
        ArgumentNullException.ThrowIfNull(owner);

        EnsureInitialized();

        CardModel[] depletedCards =
            PileType
                .GetPile(owner)
                .Cards
                .Where(card =>
                    card is IGuWormCard &&
                    !GuCardUsageRules.CanUse(card)
                )
                .ToArray();

        if (depletedCards.Length == 0)
        {
            return;
        }

        int currentTurn = owner.PlayerCombatState?.TurnNumber ?? 1;
        foreach (CardModel card in depletedCards)
        {
            if (!GuCardUsageRules.HasRecoverySchedule(card))
            {
                GuCardUsageRules.ScheduleRecovery(card, currentTurn);
            }
        }

        await CardPileCmd.Add(
            depletedCards,
            RecoveryPileType,
            CardPilePosition.Bottom,
            clonedBy: null,
            skipVisuals: false
        );
    }

    /// <summary>
    /// 每张蛊虫按照 IGuWormCard.RecoveryDelayTurns 独立记录恢复回合。
    /// 低转辅助蛊可以较快恢复，高转仙蛊则可以用更长恢复换取更强效果。
    /// </summary>
    public static async Task RestoreRecoveredGuCardsAsync(
        Player owner,
        int turnNumber
    )
    {
        ArgumentNullException.ThrowIfNull(owner);

        EnsureInitialized();

        CardModel[] allRecoveringCards =
            RecoveryPileType
                .GetPile(owner)
                .Cards
                .Where(card =>
                    card is IGuWormCard &&
                    !IsOpeningEntryPending(owner, card)
                )
                .ToArray();

        if (allRecoveringCards.Length == 0)
        {
            return;
        }

        // 兼容旧存档：没有逐牌时间戳的恢复牌，从当前回合重新开始冷却，
        // 避免在加载后被错误地立即刷新。
        foreach (CardModel card in allRecoveringCards)
        {
            if (!GuCardUsageRules.HasRecoverySchedule(card))
            {
                GuCardUsageRules.ScheduleRecovery(card, turnNumber);
            }

            await GuRecoveryEffectSystem
                .HandleRecoveryTurnStartAsync(card, turnNumber);
        }

        CardModel[] recoveredCards = allRecoveringCards
            .Where(card =>
                GuCardUsageRules.IsRecoveryReady(card, turnNumber)
            )
            .ToArray();

        if (recoveredCards.Length == 0)
        {
            return;
        }

        foreach (CardModel card in recoveredCards)
        {
            GuCardUsageRules.ResetUses(card);
        }

        await CardPileCmd.Add(
            recoveredCards,
            PileType,
            CardPilePosition.Bottom,
            clonedBy: null,
            skipVisuals: false
        );

        foreach (CardModel card in recoveredCards)
        {
            await GuRecoveryEffectSystem.HandleRecoveredAsync(card);
        }
    }

    /// <summary>
    /// Adds a generated card to the normal hand.  This is deliberately kept
    /// separate from <see cref="AddGuCardToCombat"/> for generated killer moves
    /// and other temporary cards.
    /// </summary>
    public static async Task<bool> AddGeneratedCardToHand(
        CardModel card,
        Player owner
    )
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(owner);

        CardPileAddResult result =
            await CardPileCmd.AddGeneratedCardToCombat(
                card,
                PileType.Hand,
                owner
            );

        return result.success;
    }

    public static async Task<bool> AddGeneratedCardToDiscard(
        CardModel card,
        Player owner
    )
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(owner);

        CardPileAddResult result =
            await CardPileCmd.AddGeneratedCardToCombat(
                card,
                PileType.Discard,
                owner
            );

        return result.success;
    }

    /// <summary>
    /// 将控制台或其他开发入口直接给予的卡牌按游戏规则自动放置。
    ///
    /// 战斗中，蛊虫进入蛊恢复堆并从当前回合开始计算恢复；
    /// 其他卡牌进入普通手牌。非战斗场景统一进入永久牌组。
    /// 调用方不需要、也不应再自行指定目标牌堆。
    /// </summary>
    public static PileType PlaceGrantedCardByRule(
        CardModel card,
        Player owner
    )
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(owner);

        if (!ReferenceEquals(card.Owner, owner))
        {
            throw new InvalidOperationException(
                "不能把其他玩家拥有的卡牌放入当前玩家牌堆。"
            );
        }

        if (owner.PlayerCombatState == null)
        {
            MoveCardWithoutAnimation(card, owner.Deck);
            return PileType.Deck;
        }

        if (card is IGuWormCard)
        {
            EnsureInitialized();

            GuCardUsageRules.ResetUses(card);
            GuCardUsageRules.ScheduleRecovery(
                card,
                Math.Max(1, owner.PlayerCombatState.TurnNumber)
            );

            MoveCardWithoutAnimation(
                card,
                RecoveryPileType.GetPile(owner)
            );
            return RecoveryPileType;
        }

        CardPile hand = PileType.Hand.GetPile(owner);
        MoveCardWithoutAnimation(card, hand);
        return PileType.Hand;
    }

    private static void MoveCardWithoutAnimation(
        CardModel card,
        CardPile targetPile
    )
    {
        CardPile? sourcePile = card.Pile;
        if (ReferenceEquals(sourcePile, targetPile))
        {
            return;
        }

        sourcePile?.RemoveInternal(card, silent: true);
        targetPile.AddInternal(card, silent: true);

        sourcePile?.InvokeContentsChanged();
        targetPile.InvokeContentsChanged();
    }

    private static void EnsureInitialized()
    {
        if (!_initialized)
        {
            Initialize();
        }
    }
}
