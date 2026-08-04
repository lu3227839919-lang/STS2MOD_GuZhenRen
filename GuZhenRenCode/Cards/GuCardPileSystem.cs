using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using Godot;

using STS2RitsuLib.CardPiles;

namespace GuZhenRen.Cards;

/// <summary>
/// 蛊虫专用战斗牌堆。蛊虫只会存在于蛊存放牌堆或蛊恢复堆，
/// 不会进入普通手牌。
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

        // 放在原版抽牌堆右侧
        ModCardPileDefinition definition =
            registry.RegisterOwned(
                LocalId,
                new ModCardPileSpec
                {
                    Scope = ModCardPileScope.CombatOnly,
                    Style = ModCardPileUiStyle.BottomLeft,
                    IconPath =
                        "res://GuZhenRen/images/ui/GuPaiDui.png",

                    Anchor = new ModCardPileAnchor(
                        ModCardPileAnchorKind.BottomLeftPrimary,
                        Vector2.Zero
                    ),
                }
            );

        // 放在原版弃牌堆左侧
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
    /// Moves Gu cards cloned into a combat draw pile into this pile before the
    /// opening hand is drawn.  The operation is synchronous because it runs as
    /// a postfix of <c>Player.PopulateCombatState</c>, before combat actions
    /// begin.
    /// </summary>
    internal static void InitializeGuCardsForCombat(Player owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        EnsureInitialized();

        foreach (CardPile pile in new[]
        {
            PileType.Draw.GetPile(owner),
            PileType.Discard.GetPile(owner),
            PileType.Hand.GetPile(owner),
        })
        {
            foreach (CardModel card in pile.Cards)
            {
                if (card is IGuWormCard)
                {
                    GuCardUsageRules.ResetUses(card);
                }
            }
        }

        MoveStrayGuCardsToVillage(owner);
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
                .Where(card => card is IGuWormCard)
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

    private static void EnsureInitialized()
    {
        if (!_initialized)
        {
            Initialize();
        }
    }
}
