using System.Runtime.CompilerServices;

using GuZhenRen.Cards.LiDao;
using GuZhenRen.Multiplayer;
using GuZhenRen.Powers.ZhouDao;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Settings;
using Godot;

using STS2RitsuLib.CardPiles;
using STS2RitsuLib.Utils;

namespace GuZhenRen.Cards;

/// <summary>
/// 蛊虫专用战斗区域。可用蛊虫显示在 RitsuLib ExtraHand 蛊手牌中；
/// 已冷却蛊在蛊存放牌堆等待，耗尽的蛊虫进入蛊冷却堆。
/// </summary>
public static class GuCardPileSystem
{
    public const int ActivePileCapacity = 5;

    public const string LocalId = "gu_cards";

    public const string StorageLocalId = "gu_storage";

    public const string RecoveryLocalId = "gu_discard";

    /// <summary>Fully-qualified RitsuLib card-pile ID for the Gu Hand.</summary>
    public const string PileId = "LU_GU_ZHEN_REN_CARDPILE_GU_CARDS";

    /// <summary>Fully-qualified RitsuLib ID for cooled Gu waiting to enter the Gu Hand.</summary>
    public const string StoragePileId =
        "LU_GU_ZHEN_REN_CARDPILE_GU_STORAGE";

    /// <summary>Fully-qualified RitsuLib ID for Gu cards that are still cooling down.</summary>
    public const string RecoveryPileId =
        "LU_GU_ZHEN_REN_CARDPILE_GU_DISCARD";

    public const string DiscardPileId = RecoveryPileId;

    /// <summary>
    /// 蛊封存堆：力道蛊与杀招封装材料共用的战斗内封存牌堆。
    /// 封存的蛊既不占用蛊手牌/存放牌堆，也不占用蛊冷却堆。
    /// </summary>
    public const string GuSealedLocalId = "gu_sealed";

    public const string GuSealedPileId =
        "LU_GU_ZHEN_REN_CARDPILE_GU_SEALED";

    private const string GuSealedPileIconPath =
        "res://GuZhenRen/materials/ShaZhaoMaterialPile.svg";

    private const string StoragePileIconPath =
        "res://GuZhenRen/images/ui/GuChunFangPaiDui.png";

    private const string RecoveryPileIconPath =
        "res://GuZhenRen/images/ui/GuLengQuePaiDui.png";

    private const string OpeningDrawSelectionDomain =
        "gu_pile/opening_draw";

    private const ulong StableHashOffsetBasis = 14695981039346656037UL;

    private const ulong StableHashPrime = 1099511628211UL;

    /// <summary>The runtime ExtraHand pile used as the Gu Hand.</summary>
    public static PileType PileType { get; private set; }

    /// <summary>The visible storage pile for fully cooled Gu waiting to enter the Gu Hand.</summary>
    public static PileType StoragePileType { get; private set; }

    /// <summary>The runtime pile type used only by Gu cards that are cooling down.</summary>
    public static PileType RecoveryPileType { get; private set; }

    public static PileType DiscardPileType => RecoveryPileType;

    /// <summary>
    /// 蛊封存堆：力道蛊与杀招封装材料共用（可见 UI，位于原版消耗
    /// 牌堆上方）。力道蛊战斗开始时封存于此，练力解封后进入蛊牌堆；
    /// 杀招封存材料移入此处，解体或杀招消耗后移回恢复堆。
    /// </summary>
    public static PileType GuSealedPileType { get; private set; }

    private static readonly object SyncRoot = new();

    /// <summary>
    /// 攻击药水与能力药水生成的临时蛊不占五张常规蛊手牌容量。
    /// 状态附着在战斗卡实例上，QuickSL 后仍能保持相同行为。
    /// </summary>
    private static readonly SavedAttachedState<CardModel, bool>
        TemporaryCapacityBypassState = new(
            Entry.ModId + ".temporary_gu_capacity_bypass",
            static () => false
        );

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

            // 蛊手牌以 RitsuLib ExtraHand 形式常驻显示。卡牌节点始终可见，
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

            // 新增蛊存放牌堆：只存放已冷却完毕、正在等待进入蛊手牌的蛊。
            // BottomLeftPrimary 是 RitsuLib 从原版抽牌堆向右延伸的第一槽，
            // 因此无需硬编码屏幕坐标即可紧邻原版抽牌堆。
            ModCardPileDefinition storageDefinition =
                registry.RegisterOwned(
                    StorageLocalId,
                    new ModCardPileSpec
                    {
                        Scope = ModCardPileScope.CombatOnly,
                        Style = ModCardPileUiStyle.BottomLeft,
                        IconPath = StoragePileIconPath,
                        Anchor = new ModCardPileAnchor(
                            ModCardPileAnchorKind.BottomLeftPrimary,
                            Vector2.Zero
                        ),
                        CardShouldBeVisible = true,
                    }
                );

            // 蛊冷却堆仍放在原版弃牌堆左侧，并且只保留真正处于冷却中的蛊。
            ModCardPileDefinition recoveryDefinition =
                registry.RegisterOwned(
                    RecoveryLocalId,
                    new ModCardPileSpec
                    {
                        Scope = ModCardPileScope.CombatOnly,
                        Style = ModCardPileUiStyle.BottomLeft,
                        IconPath = RecoveryPileIconPath,

                        Anchor = new ModCardPileAnchor(
                            ModCardPileAnchorKind.BottomLeftSecondary,
                            new Vector2(-200f, 0f)
                        ),
                    }
                );

            PileType = definition.PileType;
            StoragePileType = storageDefinition.PileType;
            RecoveryPileType = recoveryDefinition.PileType;

            // 蛊封存堆：力道蛊与杀招封装材料共用一个牌堆，使用
            // RitsuLib 的右下自动槽位，紧邻原版消耗牌堆。不要再叠加
            // 向上的固定偏移，否则不同分辨率下会漂入敌人区域。独立
            // SVG 图标随模组 PCK 打包，避免引用不存在的原版资源后
            // 只剩数量文本。
            ModCardPileDefinition guSealedDefinition =
                registry.RegisterOwned(
                    GuSealedLocalId,
                    new ModCardPileSpec
                    {
                        Scope = ModCardPileScope.CombatOnly,
                        Style = ModCardPileUiStyle.BottomRight,
                        IconPath = ResourceLoader.Exists(
                            GuSealedPileIconPath
                        )
                            ? GuSealedPileIconPath
                            : RecoveryPileIconPath,
                        Anchor = new ModCardPileAnchor(
                            ModCardPileAnchorKind.BottomRightPrimary,
                            // 将封存堆固定放到原版消耗牌堆正上方。
                            new Vector2(100f, -140f)
                        ),
                        HoverTipPlacement =
                            ModCardPileHoverTipPlacement.AboveButtonCentered,
                        CardShouldBeVisible = true,
                    }
                );
            GuSealedPileType = guSealedDefinition.PileType;

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
        PileType targetPile;
        if (card is ILiDaoTrainingGuCard)
        {
            LiDaoTrainingSystem.ResetForCombat(card);
            targetPile = GuSealedPileType;
        }
        else
        {
            GuCardUsageRules.ResetUses(card);
            targetPile = StoragePileType;
        }

        CardPileAddResult result =
            await CardPileCmd.AddGeneratedCardToCombat(
                card,
                targetPile,
                owner
            );

        return result.success;
    }

    /// <summary>
    /// 把药水生成的临时蛊登记为战斗生成牌。需要炼力的力道蛊进入
    /// 蛊封存堆；其他蛊统一遵守五张蛊手牌上限，满位时先进入蛊
    /// 存放牌堆，待出现空位后按现有补牌流程进入蛊手牌。
    /// </summary>
    internal static async Task<bool> AddTemporaryGuToCombat(
        CardModel card,
        Player owner,
        Player creator,
        bool bypassCapacity
    )
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(creator);

        if (card is not IGuWormCard)
        {
            throw new ArgumentException(
                "只有蛊虫牌可以进入临时蛊手牌流程。",
                nameof(card)
            );
        }

        EnsureInitialized();
        PileType targetPile;
        if (card is ILiDaoTrainingGuCard)
        {
            LiDaoTrainingSystem.ResetForCombat(card);
            targetPile = GuSealedPileType;
        }
        else
        {
            GuCardUsageRules.ResetUses(card);
            bool entersActivePile =
                bypassCapacity || HasAvailableActiveSlot(owner);
            targetPile = entersActivePile
                ? PileType
                : StoragePileType;
        }

        if (bypassCapacity && card is not ILiDaoTrainingGuCard)
        {
            TemporaryCapacityBypassState[card] = true;
        }

        CardPileAddResult result =
            await CardPileCmd.AddGeneratedCardToCombat(
                card,
                targetPile,
                creator
            );

        if (!result.success)
        {
            TemporaryCapacityBypassState.Remove(card);
        }

        return result.success;
    }

    /// <summary>
    /// 战斗初始化时先把可用蛊牌放入蛊存放牌堆。第一轮原版抽牌完成后，
    /// <see cref="BeginOpeningGuEntry"/> 会按原版抽牌节奏逐张把它们
    /// 送入蛊手牌 ExtraHand，避免两组牌堆动画同时刷新界面。
    /// </summary>
    internal static void InitializeGuCardsForCombat(Player owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        EnsureInitialized();

        CardPile storagePile = StoragePileType.GetPile(owner);
        CardPile recoveryPile = RecoveryPileType.GetPile(owner);
        CardPile guSealedPile = GuSealedPileType.GetPile(owner);
        CardPile[] combatPiles =
        [
            PileType.Draw.GetPile(owner),
            PileType.Discard.GetPile(owner),
            PileType.Hand.GetPile(owner),
            PileType.GetPile(owner),
            storagePile,
            recoveryPile,
            guSealedPile,
        ];

        CardModel[] guCards = combatPiles
            .SelectMany(static pile => pile.Cards)
            .Where(static card => card is IGuWormCard)
            .Distinct()
            .ToArray();

        HashSet<CardPile> changedPiles = [];
        foreach (CardModel card in guCards)
        {
            CardPile targetPile;
            if (card is ILiDaoTrainingGuCard)
            {
                LiDaoTrainingSystem.ResetForCombat(card);
                targetPile = guSealedPile;
            }
            else
            {
                GuCardUsageRules.ResetUses(card);
                targetPile = storagePile;
            }

            CardPile? sourcePile = card.Pile;
            if (sourcePile != null &&
                !ReferenceEquals(sourcePile, targetPile))
            {
                sourcePile.RemoveInternal(card, silent: true);
                changedPiles.Add(sourcePile);
                targetPile.AddInternal(card, silent: true);
                changedPiles.Add(targetPile);
            }
        }

        foreach (CardPile changedPile in changedPiles)
        {
            changedPile.InvokeContentsChanged();
        }

        CardModel[] openingCandidates = guCards
            .Where(static card => card is not ILiDaoTrainingGuCard)
            .ToArray();
        CardModel[] openingCards = DrawRandomGuCards(
            owner,
            openingCandidates,
            ActivePileCapacity,
            OpeningDrawSelectionDomain
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
            string selectedCards = string.Join(
                ", ",
                openingCards.Select(card =>
                    $"{card.Id}#" +
                    GuZhenRenDeterminism.GetCardNetworkId(card)
                )
            );
            Entry.Logger.Info(
                $"[蛊牌入场] 共 {guCards.Length} 张蛊牌；随机选取 " +
                $"{openingCards.Length} 张进入蛊手牌，" +
                $"{openingCandidates.Length - openingCards.Length} 张留在蛊存放牌堆待命，" +
                $"{guCards.Length - openingCandidates.Length} 张力道蛊进入蛊封存堆；" +
                $"确定性选择=[{selectedCards}]。"
            );
        }
    }

    /// <summary>
    /// 在首轮 <c>CardPileCmd.DrawInternal</c> 完成后启动蛊牌入场。
    /// 每张牌都独立等待原生移牌动画，与原版逐张抽牌的执行顺序一致。
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
        CardPile storagePile = StoragePileType.GetPile(owner);
        CardModel[] cards = state.Cards
            .Where(card =>
                card is IGuWormCard &&
                ReferenceEquals(card.Pile, storagePile)
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
                $"[蛊牌入场] 原版起手抽牌完成后，逐张播放 " +
                $"{cards.Length} 张蛊牌的存放堆→蛊手牌原生移牌动画。"
            );

            foreach (CardModel card in cards)
            {
                if (!ReferenceEquals(card.Pile, storagePile))
                {
                    continue;
                }

                CardPileAddResult result =
                    await AddGuCardToActivePileSequentiallyAsync(card);

                if (!result.success)
                {
                    throw new InvalidOperationException(
                        $"蛊牌 {card.Id} 无法进入蛊手牌。"
                    );
                }
            }
        }
        catch (Exception exception)
        {
            Entry.Logger.Error(
                $"[蛊牌入场] 动画执行失败，已回退为无动画入场：{exception}"
            );

            CardPile guPile = PileType.GetPile(owner);
            foreach (CardModel card in cards)
            {
                if (!ReferenceEquals(card.Pile, storagePile))
                {
                    continue;
                }

                storagePile.RemoveInternal(card, silent: true);
                guPile.AddInternal(card, silent: true);
            }

            storagePile.InvokeContentsChanged();
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

    /// <summary>
    /// RitsuLib 的 ExtraHand 入场补丁会在创建卡牌节点后立即结束
    /// <see cref="CardPileCmd.Add(CardModel, PileType, CardPilePosition, AbstractModel, bool)"/>
    /// 返回的任务，因此仅逐张 await 仍会在同一帧启动全部蛊牌动画。
    /// 原版手牌抽取会让每张牌的入场 Tween 按加速档位持续一小段时间；
    /// 这里复用完全相同的间隔，使下一张蛊牌在下一段抽牌节奏中入场。
    /// </summary>
    private static async Task<CardPileAddResult>
        AddGuCardToActivePileSequentiallyAsync(CardModel card)
    {
        CardPileAddResult result = await CardPileCmd.Add(
            card,
            PileType,
            CardPilePosition.Bottom,
            clonedBy: null,
            skipVisuals: false
        );

        // 原版不会为其他玩家的手牌动画阻塞当前客户端。
        if (result.success && LocalContext.IsMine(card))
        {
            float nativeHandEntryInterval =
                SaveManager.Instance.PrefsSave.FastMode switch
                {
                    FastModeType.Instant => 0.01f,
                    FastModeType.Fast => 0.1f,
                    _ => 0.25f,
                };
            await Cmd.Wait(nativeHandEntryInterval);
        }

        return result;
    }

    /// <summary>
    /// 起手蛊牌仍在逐张进入 ExtraHand 时，牌面节点可能已经创建，但
    /// CardPileCmd 的整段入场事务尚未完成。此窗口内不能开始手动出牌，
    /// 否则 RitsuLib 的临时 Hand 迁移会与尚在执行的入场迁移交错。
    /// </summary>
    internal static bool IsOpeningEntryPending(Player owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        if (!OpeningEntryStates.TryGetValue(owner, out var state))
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

        CardPile storagePile = StoragePileType.GetPile(owner);
        CardPile recoveryPile = RecoveryPileType.GetPile(owner);
        CardPile guSealedPile = GuSealedPileType.GetPile(owner);
        MoveActiveOverflowToStorageOrRecovery(owner);
        HashSet<CardPile> changedPiles = [];

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
                changedPiles.Add(sourcePile);

                if (card is ILiDaoTrainingGuCard &&
                    !LiDaoTrainingSystem.IsUnsealed(card))
                {
                    guSealedPile.AddInternal(card, silent: true);
                    changedPiles.Add(guSealedPile);
                    continue;
                }

                if (GuCardUsageRules.CanUse(card) &&
                    !GuCardUsageRules.HasRecoverySchedule(card))
                {
                    // 已可用的蛊统一进入蛊存放牌堆，由回合开始的补牌流程
                    // 按冷却完成顺序送入蛊手牌。
                    storagePile.AddInternal(card, silent: true);
                    changedPiles.Add(storagePile);
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
                    changedPiles.Add(recoveryPile);
                }
            }
        }

        foreach (CardPile changedPile in changedPiles)
        {
            changedPile.InvokeContentsChanged();
        }
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

        // 第一回合的起手入场与原版抽牌并行。此时不得额外补蛊手牌，
        // 否则稍后的起手入场动画会突破五张上限。
        if (IsOpeningEntryPending(owner))
        {
            return;
        }

        CardPile recoveryPile = RecoveryPileType.GetPile(owner);
        CardPile storagePile = StoragePileType.GetPile(owner);
        CardModel[] coolingCards = recoveryPile.Cards
            .Where(static card => card is IGuWormCard)
            // 被杀招封装的材料不进入恢复循环。
            .Where(card =>
                !ShaZhaoTuiYanSystem.IsMaterialSealed(card)
            )
            .ToArray();

        // 兼容旧存档：旧版恢复堆可能混有“已恢复但因蛊手牌已满而待命”
        // 的蛊。新版将它们迁移到专门的蛊存放牌堆，不重新开始冷却。
        CardModel[] legacyReadyCards = coolingCards
            .Where(card =>
                GuCardUsageRules.CanUse(card) &&
                !GuCardUsageRules.HasRecoverySchedule(card)
            )
            .OrderBy(GuCardUsageRules.GetRecoveryCompletedTurn)
            .ThenBy(GuZhenRenDeterminism.GetCardNetworkId)
            .ToArray();

        foreach (CardModel card in legacyReadyCards)
        {
            await MoveCardToPileAsync(
                card,
                StoragePileType,
                skipVisuals: false
            );
        }

        CardModel[] activelyCoolingCards = recoveryPile.Cards
            .Where(static card => card is IGuWormCard)
            .Where(card =>
                !ShaZhaoTuiYanSystem.IsMaterialSealed(card)
            )
            .ToArray();

        foreach (CardModel card in activelyCoolingCards)
        {
            if (!GuCardUsageRules.HasRecoverySchedule(card))
            {
                GuCardUsageRules.ScheduleRecovery(card, turnNumber);
            }

            await GuRecoveryEffectSystem
                .HandleRecoveryTurnStartAsync(card, turnNumber);
        }

        CardModel[] recoveredCards = activelyCoolingCards
            .Where(card =>
                GuCardUsageRules.IsRecoveryReady(card, turnNumber)
            )
            .OrderBy(card =>
                GuCardUsageRules.GetRecoveryCompletedTurn(card)
            )
            .ThenBy(GuZhenRenDeterminism.GetCardNetworkId)
            .ToArray();

        foreach (CardModel card in recoveredCards)
        {
            GuCardUsageRules.ResetUses(card);
            GuCardUsageRules.MarkRecoveryCompleted(
                card,
                turnNumber
            );
            await GuRecoveryEffectSystem.HandleRecoveredAsync(card);

            // 冷却完成先回到“蛊存放牌堆”。使用 CardPileCmd 的原生
            // 飞行动画，而不是直接改 CardPile 内部列表。
            await MoveCardToPileAsync(
                card,
                StoragePileType,
                skipVisuals: false
            );
            await ZhouDaoPowerSystem.NotifyGuRecoveredAsync(
                card,
                acceleratedBySuiMan: false
            );
        }

        CardModel[] readyCards = storagePile.Cards
            .Where(card =>
                card is IGuWormCard &&
                GuCardUsageRules.CanUse(card) &&
                !GuCardUsageRules.HasRecoverySchedule(card) &&
                !ShaZhaoTuiYanSystem.IsMaterialSealed(card)
            )
            // 先冷却完成的先进入蛊手牌；同回合完成时用网络 ID 稳定定序。
            .OrderBy(GuCardUsageRules.GetRecoveryCompletedTurn)
            .ThenBy(GuZhenRenDeterminism.GetCardNetworkId)
            .Take(GetAvailableActiveSlots(owner))
            .ToArray();

        foreach (CardModel card in readyCards)
        {
            await AddGuCardToActivePileSequentiallyAsync(card);
        }
    }


    /// <summary>
    /// 宙道【岁满】将一只正在恢复的蛊向前推进指定回合；若推进后
    /// 已在当前回合就绪，则立即完成恢复，并在有空位时返回蛊手牌。
    /// </summary>
    internal static async Task<bool> AccelerateRecoveryAsync(
        Player owner,
        CardModel card,
        int turnNumber,
        int turns = 1
    )
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(card);
        EnsureInitialized();

        CardPile recoveryPile = RecoveryPileType.GetPile(owner);
        if (card is not IGuWormCard ||
            !ReferenceEquals(card.Pile, recoveryPile) ||
            !GuCardUsageRules.HasRecoverySchedule(card) ||
            ShaZhaoTuiYanSystem.IsMaterialSealed(card))
        {
            return false;
        }

        int nextReady = GuCardUsageRules.ReduceRecoveryReadyTurn(
            card,
            turns,
            turnNumber
        );
        if (nextReady <= 0 || nextReady > turnNumber)
        {
            return true;
        }

        GuCardUsageRules.ResetUses(card);
        GuCardUsageRules.MarkRecoveryCompleted(card, turnNumber);
        await GuRecoveryEffectSystem.HandleRecoveredAsync(card);
        await MoveCardToPileAsync(
            card,
            StoragePileType,
            skipVisuals: false
        );
        await ZhouDaoPowerSystem.NotifyGuRecoveredAsync(
            card,
            acceleratedBySuiMan: true
        );

        if (GetAvailableActiveSlots(owner) > 0 &&
            ReferenceEquals(card.Pile, StoragePileType.GetPile(owner)))
        {
            await AddGuCardToActivePileSequentiallyAsync(card);
        }

        return true;
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

        if (HandCapacityExemptionPatch.IsCapacityExempt(card))
        {
            return await HandCapacityExemptionPatch
                .AddGeneratedExemptToHandAsync(card, owner);
        }

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

        if (card is ILiDaoTrainingGuCard)
        {
            EnsureInitialized();
            LiDaoTrainingSystem.ResetForCombat(card);
            MoveCardWithoutAnimation(
                card,
                GuSealedPileType.GetPile(owner)
            );
            return GuSealedPileType;
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

    /// <summary>
    /// Moves an existing combat card through the native pile command.
    /// Direct internal moves can leave an ExtraHand holder alive, so use
    /// this path when a card enters or leaves a UI-backed custom pile.
    /// </summary>
    internal static async Task MoveCardToPileAsync(
        CardModel card,
        PileType targetPile,
        bool skipVisuals = true
    )
    {
        ArgumentNullException.ThrowIfNull(card);

        EnsureInitialized();

        CardPile destination = targetPile.GetPile(card.Owner);
        if (ReferenceEquals(card.Pile, destination))
        {
            return;
        }

        if (targetPile == PileType.Hand &&
            HandCapacityExemptionPatch.IsCapacityExempt(card))
        {
            await HandCapacityExemptionPatch.MoveExemptToHandAsync(
                card,
                skipVisuals
            );
            return;
        }

        try
        {
            await CardPileCmd.Add(
                [card],
                targetPile,
                CardPilePosition.Bottom,
                clonedBy: null,
                skipVisuals: skipVisuals
            );
        }
        catch (Exception exception)
        {
            if (!ReferenceEquals(card.Pile, destination))
            {
                MoveCardWithoutAnimation(card, destination);
            }

            Entry.Logger.Warn(
                $"[蛊牌堆] 原生移牌失败，已回退内部移牌：" +
                $"card={card.Id}, target={targetPile}, " +
                $"error={exception.Message}"
            );
        }

        if (!ReferenceEquals(card.Pile, destination))
        {
            MoveCardWithoutAnimation(card, destination);
        }
    }

    internal static bool HasAvailableActiveSlot(Player owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        EnsureInitialized();
        return GetAvailableActiveSlots(owner) > 0;
    }

    private static bool BypassesActivePileCapacity(CardModel card) =>
        TemporaryCapacityBypassState.GetValueOrDefault(
            card,
            defaultValue: false
        );

    private static int GetActiveGuCount(Player owner) =>
        PileType
            .GetPile(owner)
            .Cards
            .Count(card =>
                card is IGuWormCard &&
                !BypassesActivePileCapacity(card)
            );

    private static int GetAvailableActiveSlots(Player owner) =>
        Math.Max(0, ActivePileCapacity - GetActiveGuCount(owner));

    /// <summary>
    /// 兼容旧存档或其他模组直接移动蛊牌的情况，保证存放堆绝不超过
    /// 五张。可用的溢出蛊回到蛊存放牌堆；耗尽牌继续进入蛊冷却堆。
    /// </summary>
    private static void MoveActiveOverflowToStorageOrRecovery(Player owner)
    {
        CardPile guPile = PileType.GetPile(owner);
        CardPile storagePile = StoragePileType.GetPile(owner);
        CardPile recoveryPile = RecoveryPileType.GetPile(owner);
        CardModel[] overflowCards = guPile.Cards
            .Where(card =>
                card is IGuWormCard &&
                !BypassesActivePileCapacity(card)
            )
            .Skip(ActivePileCapacity)
            .ToArray();

        if (overflowCards.Length == 0)
        {
            return;
        }

        int currentTurn = owner.PlayerCombatState?.TurnNumber ?? 1;
        bool storageChanged = false;
        bool recoveryChanged = false;
        foreach (CardModel card in overflowCards)
        {
            guPile.RemoveInternal(card, silent: true);

            if (GuCardUsageRules.CanUse(card) &&
                !GuCardUsageRules.HasRecoverySchedule(card))
            {
                storagePile.AddInternal(card, silent: true);
                storageChanged = true;
                continue;
            }

            if (!GuCardUsageRules.HasRecoverySchedule(card))
            {
                GuCardUsageRules.ScheduleRecovery(card, currentTurn);
            }

            recoveryPile.AddInternal(card, silent: true);
            recoveryChanged = true;
        }

        guPile.InvokeContentsChanged();
        if (storageChanged)
        {
            storagePile.InvokeContentsChanged();
        }
        if (recoveryChanged)
        {
            recoveryPile.InvokeContentsChanged();
        }
    }

    /// <summary>
    /// 使用不依赖可变 RNG 计数器的确定性洗牌抽取蛊牌。
    ///
    /// 多人 QuickSL 会在两端重建 Player 与战斗对象；模组玩家 RNG 的
    /// 流位置并不属于原版战斗同步快照，因此即使候选顺序完全相同，
    /// 重载后也可能抽出不同的五张牌。这里直接由同步的玩家网络 ID、
    /// 楼层、候选卡牌属性与网络卡牌 ID 派生洗牌种子。相同战斗无论
    /// 创建或重载多少次都会得到同一结果，同时不同楼层仍有不同排列。
    /// </summary>
    private static CardModel[] DrawRandomGuCards(
        Player owner,
        IEnumerable<CardModel> candidates,
        int maximumCount,
        string selectionDomain
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
            // 同名、同转、同升级的多张卡仍必须使用跨端一致的最终键；
            // 否则稳定排序会继承各端牌堆枚举顺序，随机索引可能选到
            // 不同的战斗实例。
            .ThenBy(GuZhenRenDeterminism.GetCardNetworkId)
            .ToArray();

        int drawCount = Math.Clamp(maximumCount, 0, pool.Length);
        if (drawCount == 0)
        {
            return [];
        }

        if (pool.Length > 1)
        {
            ulong selectionState = BuildSelectionSeed(
                owner,
                pool,
                selectionDomain
            );

            for (int index = 0;
                 index < drawCount && index < pool.Length - 1;
                 index++)
            {
                int remaining = pool.Length - index;
                int selectedIndex = index + (int)(
                    NextDeterministicValue(ref selectionState) %
                    (ulong)remaining
                );
                (pool[index], pool[selectedIndex]) =
                    (pool[selectedIndex], pool[index]);
            }
        }

        return pool.Take(drawCount).ToArray();
    }

    private static ulong BuildSelectionSeed(
        Player owner,
        IReadOnlyList<CardModel> pool,
        string selectionDomain
    )
    {
        ulong hash = StableHashOffsetBasis;
        AddStableHash(ref hash, Entry.ModId);
        AddStableHash(ref hash, selectionDomain);
        AddStableHash(ref hash, owner.NetId);
        AddStableHash(
            ref hash,
            unchecked((ulong)owner.RunState.TotalFloor)
        );

        foreach (CardModel card in pool)
        {
            AddStableHash(ref hash, card.Id.ToString());
            AddStableHash(
                ref hash,
                GuZhenRenDeterminism.GetCardNetworkId(card)
            );
            AddStableHash(
                ref hash,
                unchecked((ulong)
                    GuZhenRenDeterminism.GetDeckCardIndex(card))
            );
            AddStableHash(
                ref hash,
                unchecked((ulong)card.CurrentUpgradeLevel)
            );
            AddStableHash(
                ref hash,
                unchecked((ulong)(
                    card is IGuRankProvider rankProvider
                        ? rankProvider.GuRank
                        : 0
                ))
            );
        }

        return hash;
    }

    private static void AddStableHash(
        ref ulong hash,
        string value
    )
    {
        foreach (char character in value)
        {
            AddStableHash(ref hash, character);
        }

        // 字符串边界也参与哈希，避免 ["ab", "c"] 与 ["a", "bc"]
        // 产生同一个输入序列。
        AddStableHash(ref hash, char.MaxValue);
    }

    private static void AddStableHash(
        ref ulong hash,
        ulong value
    )
    {
        unchecked
        {
            for (int byteIndex = 0;
                 byteIndex < sizeof(ulong);
                 byteIndex++)
            {
                hash ^= (byte)value;
                hash *= StableHashPrime;
                value >>= 8;
            }
        }
    }

    private static ulong NextDeterministicValue(ref ulong state)
    {
        // SplitMix64：这里只用作确定性排列，不承担安全随机用途。
        unchecked
        {
            state += 0x9E3779B97F4A7C15UL;
            ulong value = state;
            value = (value ^ (value >> 30)) *
                0xBF58476D1CE4E5B9UL;
            value = (value ^ (value >> 27)) *
                0x94D049BB133111EBUL;
            return value ^ (value >> 31);
        }
    }

    private static void EnsureInitialized()
    {
        if (!_initialized)
        {
            Initialize();
        }
    }
}
