using System.Runtime.CompilerServices;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using Godot;

using STS2RitsuLib;
using STS2RitsuLib.CardPiles;

namespace GuZhenRen.Cards;

/// <summary>
/// 蛊虫专用战斗区域。可用蛊虫显示在 RitsuLib ExtraHand 蛊手牌中，
/// 耗尽的蛊虫进入蛊恢复堆；它们不会长期进入原版普通手牌。
/// </summary>
public static class GuCardPileSystem
{
    public const int ActivePileCapacity = 5;

    public const string LocalId = "gu_cards";

    public const string RecoveryLocalId = "gu_discard";

    /// <summary>Fully-qualified RitsuLib card-pile ID used by localization.</summary>
    public const string PileId = "GU_ZHEN_REN_CARDPILE_GU_CARDS";

    /// <summary>Fully-qualified RitsuLib ID for the recovering Gu pile.</summary>
    public const string RecoveryPileId =
        "GU_ZHEN_REN_CARDPILE_GU_DISCARD";

    public const string DiscardPileId = RecoveryPileId;

    /// <summary>
    /// 杀招封装材料的隐藏牌堆：Headless 样式，无任何 UI，
    /// 封存的材料既不占用蛊存放堆也不占用蛊恢复堆。
    /// </summary>
    public const string MaterialPileId =
        "GU_ZHEN_REN_CARDPILE_SHA_ZHAO_MATERIAL";

    private const string OpeningDrawRngStreamId =
        "gu_pile/opening_draw";

    private const string RecoveredDrawRngStreamId =
        "gu_pile/recovered_draw";

    /// <summary>The runtime pile type assigned by RitsuLib.</summary>
    public static PileType PileType { get; private set; }

    /// <summary>The runtime pile type used by Gu cards with no uses left.</summary>
    public static PileType RecoveryPileType { get; private set; }

    public static PileType DiscardPileType => RecoveryPileType;

    /// <summary>
    /// 杀招封装材料的隐藏牌堆（无 UI）。封存后材料从蛊存放堆/恢复堆
    /// 移入此处，不占用蛊牌堆容量；解体或杀招消耗后移回。
    /// </summary>
    public static PileType MaterialPileType { get; private set; }

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
            // 但只有普通手牌中存在可用“催动”时才能开始原生出牌/目标选择。
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

            // 杀招封装材料的隐藏牌堆：Headless 无 UI，封存材料不占用
            // 蛊存放堆与蛊恢复堆的容量与显示。
            ModCardPileDefinition materialDefinition =
                registry.RegisterOwned(
                    MaterialPileId,
                    new ModCardPileSpec
                    {
                        Scope = ModCardPileScope.CombatOnly,
                        Style = ModCardPileUiStyle.Headless,
                    }
                );
            MaterialPileType = materialDefinition.PileType;

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

        PileType targetPile =
            GetAvailableActiveSlots(owner) > 0
                ? PileType
                : RecoveryPileType;

        CardPileAddResult result =
            await CardPileCmd.AddGeneratedCardToCombat(
                card,
                targetPile,
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

        CardModel[] openingCards = DrawRandomGuCards(
            owner,
            guCards,
            ActivePileCapacity,
            OpeningDrawRngStreamId
        );

        OpeningEntryStates.Remove(owner);
        OpeningEntryState state = OpeningEntryStates.GetValue(
            owner,
            static _ => new OpeningEntryState()
        );
        state.Cards = openingCards;
        state.Started = false;
        state.Completed = openingCards.Length == 0;
        state.AnimationTask = null;

        if (guCards.Length > 0)
        {
            Entry.Logger.Info(
                $"[蛊牌入场] 共 {guCards.Length} 张蛊牌；随机选取 " +
                $"{openingCards.Length} 张进入蛊存放牌堆，" +
                $"{guCards.Length - openingCards.Length} 张留在恢复堆待命。"
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
            .Take(GetAvailableActiveSlots(owner))
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

    private static bool IsOpeningEntryPending(Player owner)
    {
        if (!OpeningEntryStates.TryGetValue(owner, out var state) ||
            state.Completed)
        {
            return false;
        }

        lock (state)
        {
            return !state.Completed;
        }
    }

    internal static void MoveStrayGuCardsToVillage(Player owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        EnsureInitialized();

        CardPile guPile = PileType.GetPile(owner);
        CardPile recoveryPile = RecoveryPileType.GetPile(owner);
        MoveActiveOverflowToRecovery(owner);
        int availableSlots = GetAvailableActiveSlots(owner);

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
                    if (availableSlots > 0)
                    {
                        guPile.AddInternal(card, silent: true);
                        availableSlots--;
                    }
                    else
                    {
                        // 可用但超过五张上限：作为“已恢复待命”蛊保留，
                        // 不建立冷却时间戳。
                        recoveryPile.AddInternal(card, silent: true);
                    }
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

        // 第一回合的起手入场与原版抽牌并行。此时不得让恢复流程先把
        // 待命蛊塞满存放堆，否则稍后的入场动画会突破五张上限。
        if (IsOpeningEntryPending(owner))
        {
            return;
        }

        CardPile recoveryPile = RecoveryPileType.GetPile(owner);
        CardModel[] allRecoveringCards =
            recoveryPile.Cards
                .Where(static card => card is IGuWormCard)
                // 被杀招封装的材料不进入恢复循环。
                .Where(card =>
                    !ShaZhaoTuiYanSystem.IsMaterialSealed(card)
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
            // 没有冷却时间戳且已有可用次数，表示该牌已经恢复，只因
            // 五张上限暂存在恢复堆。它无需再次开始冷却或重复触发恢复效果。
            if (GuCardUsageRules.CanUse(card) &&
                !GuCardUsageRules.HasRecoverySchedule(card))
            {
                continue;
            }

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
            await GuRecoveryEffectSystem.HandleRecoveredAsync(card);
        }

        CardModel[] readyCards = recoveryPile.Cards
            .Where(card =>
                card is IGuWormCard &&
                GuCardUsageRules.CanUse(card) &&
                !GuCardUsageRules.HasRecoverySchedule(card)
            )
            .ToArray();

        int availableSlots = GetAvailableActiveSlots(owner);
        if (availableSlots <= 0 || readyCards.Length == 0)
        {
            return;
        }

        CardModel[] drawnCards = DrawRandomGuCards(
            owner,
            readyCards,
            availableSlots,
            RecoveredDrawRngStreamId
        );

        await CardPileCmd.Add(
            drawnCards,
            PileType,
            CardPilePosition.Bottom,
            clonedBy: null,
            skipVisuals: false
        );
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
        targetPile.InvokeContentsChanged();    }

    /// <summary>
    /// 在蛊牌堆之间移动卡牌（供杀招材料封装/返还等内部流程使用）。
    /// </summary>
    internal static void MoveCardToPile(
        CardModel card,
        CardPile targetPile
    )
    {
        MoveCardWithoutAnimation(card, targetPile);
    }

    private static int GetActiveGuCount(Player owner) =>
        PileType
            .GetPile(owner)
            .Cards
            .Count(static card => card is IGuWormCard);

    private static int GetAvailableActiveSlots(Player owner) =>
        Math.Max(0, ActivePileCapacity - GetActiveGuCount(owner));

    /// <summary>
    /// 兼容旧存档或其他模组直接移动蛊牌的情况，保证存放堆绝不超过
    /// 五张。可用的溢出蛊作为已恢复待命牌保留；耗尽牌继续正常冷却。
    /// </summary>
    private static void MoveActiveOverflowToRecovery(Player owner)
    {
        CardPile guPile = PileType.GetPile(owner);
        CardPile recoveryPile = RecoveryPileType.GetPile(owner);
        CardModel[] overflowCards = guPile.Cards
            .Where(static card => card is IGuWormCard)
            .Skip(ActivePileCapacity)
            .ToArray();

        if (overflowCards.Length == 0)
        {
            return;
        }

        int currentTurn = owner.PlayerCombatState?.TurnNumber ?? 1;
        foreach (CardModel card in overflowCards)
        {
            guPile.RemoveInternal(card, silent: true);

            if (!GuCardUsageRules.CanUse(card) &&
                !GuCardUsageRules.HasRecoverySchedule(card))
            {
                GuCardUsageRules.ScheduleRecovery(card, currentTurn);
            }

            recoveryPile.AddInternal(card, silent: true);
        }

        guPile.InvokeContentsChanged();
        recoveryPile.InvokeContentsChanged();
    }

    /// <summary>
    /// 使用按模组、玩家和用途隔离的确定性 RNG 随机抽取蛊牌。
    /// 候选先按可保存属性稳定排序，确保多人端以相同顺序推进随机流。
    /// 即使全部候选都能进入，也会随机排列入场顺序。
    /// </summary>
    private static CardModel[] DrawRandomGuCards(
        Player owner,
        IEnumerable<CardModel> candidates,
        int maximumCount,
        string rngStreamId
    )
    {
        CardModel[] pool = candidates
            .Where(static card => card is IGuWormCard)
            .OrderBy(
                static card => card.Id.ToString(),
                StringComparer.Ordinal
            )
            .ThenBy(static card =>
                card is IGuRankProvider rankProvider
                    ? rankProvider.GuRank
                    : 0
            )
            .ThenBy(static card => card.CurrentUpgradeLevel)
            .ToArray();

        int drawCount = Math.Clamp(maximumCount, 0, pool.Length);
        if (drawCount == 0)
        {
            return [];
        }

        if (pool.Length > 1)
        {
            Rng rng = RitsuLibFramework.GetModPlayerRng(
                owner,
                Entry.ModId,
                rngStreamId
            );

            for (int index = 0;
                 index < drawCount && index < pool.Length - 1;
                 index++)
            {
                int selectedIndex =
                    index + rng.NextInt(pool.Length - index);
                (pool[index], pool[selectedIndex]) =
                    (pool[selectedIndex], pool[index]);
            }
        }

        return pool.Take(drawCount).ToArray();
    }

    private static void EnsureInitialized()
    {
        if (!_initialized)
        {
            Initialize();
        }
    }
}
