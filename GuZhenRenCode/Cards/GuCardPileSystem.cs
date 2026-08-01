using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using Godot;

using STS2RitsuLib.CardPiles;

namespace GuZhenRen.Cards;

/// <summary>
/// Combat pile reserved for Gu cards.
///
/// The pile is intentionally separate from the normal hand.  Code that creates a
/// temporary/generated card should use <see cref="AddGeneratedCardToHand"/> so
/// generated cards continue to appear in the player's hand.
/// </summary>
public static class GuCardPileSystem
{
    public const string LocalId = "gu_cards";

    public const string DiscardLocalId = "gu_discard";

    /// <summary>Fully-qualified RitsuLib card-pile ID used by localization.</summary>
    public const string PileId = "GU_ZHEN_REN_CARDPILE_GU_CARDS";

    /// <summary>Fully-qualified RitsuLib ID for the spent Gu pile.</summary>
    public const string DiscardPileId =
        "GU_ZHEN_REN_CARDPILE_GU_DISCARD";

    /// <summary>The runtime pile type assigned by RitsuLib.</summary>
    public static PileType PileType { get; private set; }

    /// <summary>The runtime pile type used by Gu cards with no uses left.</summary>
    public static PileType DiscardPileType { get; private set; }

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

            ModCardPileDefinition definition = registry.RegisterOwned(
                LocalId,
                new ModCardPileSpec
                {
                    Scope = ModCardPileScope.CombatOnly,
                    Style = ModCardPileUiStyle.BottomLeft,
                    // Keep the original bottom-left layout and default card-flight animation.
                    IconPath = "res://GuZhenRen/images/ui/GuPaiDui.png",
                    Anchor = new ModCardPileAnchor(
                        ModCardPileAnchorKind.BottomLeftPrimary,
                        Vector2.Zero
                    ),
                }
            );

            ModCardPileDefinition discardDefinition =
                registry.RegisterOwned(
                    DiscardLocalId,
                    new ModCardPileSpec
                    {
                        Scope = ModCardPileScope.CombatOnly,
                        Style = ModCardPileUiStyle.BottomLeft,
                        // Place the Gu discard pile beside the base-game discard pile.
                        // RitsuLib's default pile movement keeps the vanilla animation.
                        IconPath =
                            "res://GuZhenRen/images/ui/QiPaiDui.png",
                        Anchor = new ModCardPileAnchor(
                            ModCardPileAnchorKind.BottomLeftSecondary,
                            Vector2.Zero
                        ),
                    }
                );

            PileType = definition.PileType;
            DiscardPileType = discardDefinition.PileType;
            _initialized = true;
        }
    }

    public static void Uninitialize()
    {
        lock (SyncRoot)
        {
            _initialized = false;
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
    internal static void MoveGuCardsToPile(Player owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        EnsureInitialized();

        CardPile drawPile = PileType.Draw.GetPile(owner);
        CardPile guPile = PileType.GetPile(owner);
        CardPile guDiscardPile = DiscardPileType.GetPile(owner);

        CardModel[] guCards =
            drawPile
                .Cards
                .Where(card => card is IGuWormCard)
                .ToArray();

        if (guCards.Length == 0)
        {
            return;
        }

        foreach (CardModel card in guCards)
        {
            drawPile.RemoveInternal(card, silent: true);

            if (GuCardUsageRules.CanUse(card))
            {
                guPile.AddInternal(card, silent: true);
            }
            else
            {
                guDiscardPile.AddInternal(card, silent: true);
            }
        }

        drawPile.InvokeContentsChanged();
        guPile.InvokeContentsChanged();
        guDiscardPile.InvokeContentsChanged();
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

        if (card is not IGuWormCard guCard)
        {
            return PileType;
        }

        int remainingUses = Math.Max(
            0,
            guCard.MaxUsesPerTurn -
            GuCardUsageRules.CountUsesThisTurn(card) -
            1
        );

        return remainingUses == 0
            ? DiscardPileType
            : PileType;
    }

    /// <summary>
    /// Moves every Gu card that has no uses left out of the active Gu pile.
    /// CardPileCmd supplies the same pile-flight animation used by the base game.
    /// </summary>
    public static async Task DiscardDepletedGuCardsAsync(Player owner)
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

        await CardPileCmd.Add(
            depletedCards,
            DiscardPileType,
            CardPilePosition.Bottom,
            clonedBy: null,
            skipVisuals: false
        );
    }

    /// <summary>
    /// At the beginning of a new player turn, returns Gu cards whose per-turn
    /// uses have reset to the active Gu pile.  Permanently unusable cards remain
    /// in the Gu discard pile.
    /// </summary>
    public static async Task RestoreAvailableGuCardsAsync(Player owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        EnsureInitialized();

        CardModel[] availableCards =
            DiscardPileType
                .GetPile(owner)
                .Cards
                .Where(card =>
                    card is IGuWormCard &&
                    GuCardUsageRules.CanUse(card)
                )
                .ToArray();

        if (availableCards.Length == 0)
        {
            return;
        }

        await CardPileCmd.Add(
            availableCards,
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

    private static void EnsureInitialized()
    {
        if (!_initialized)
        {
            Initialize();
        }
    }
}
