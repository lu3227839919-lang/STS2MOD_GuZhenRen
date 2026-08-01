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

    /// <summary>Fully-qualified RitsuLib card-pile ID used by localization.</summary>
    public const string PileId = "GU_ZHEN_REN_CARDPILE_GU_CARDS";

    /// <summary>The runtime pile type assigned by RitsuLib.</summary>
    public static PileType PileType { get; private set; }

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
                    // Reuse the game's draw-pile marker so the custom pile reads
                    // as the same kind of source pile in combat UI and flight
                    // animations.
                    IconPath = "res://images/packed/combat_ui/draw_pile.png",
                    Anchor = new ModCardPileAnchor(
                        ModCardPileAnchorKind.BottomLeftPrimary,
                        Vector2.Zero
                    ),
                }
            );

            PileType = definition.PileType;
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
            guPile.AddInternal(card, silent: true);
        }

        drawPile.InvokeContentsChanged();
        guPile.InvokeContentsChanged();
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
