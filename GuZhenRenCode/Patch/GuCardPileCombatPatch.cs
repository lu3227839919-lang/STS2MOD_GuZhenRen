using System.Reflection;

using GuZhenRen.Cards;

using HarmonyLib;

using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Random;

namespace GuZhenRen.Patches;

/// <summary>
/// Moves every Gu card cloned into a combat draw pile into the dedicated Gu
/// pile before the first draw.  This keeps Gu cards out of normal hand draws.
/// </summary>
internal static class GuCardPileCombatPatch
{
    private const string HarmonyId =
        Entry.ModId + ".GuCardPileCombat";

    private static bool _initialized;

    internal static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        MethodInfo? populateCombatState =
            AccessTools.DeclaredMethod(
                typeof(Player),
                nameof(Player.PopulateCombatState),
                [typeof(Rng), typeof(CombatState)]
            );

        if (populateCombatState == null)
        {
            throw new MissingMethodException(
                "Player.PopulateCombatState was not found."
            );
        }

        MethodInfo? drawInternal =
            AccessTools.DeclaredMethod(
                typeof(CardPileCmd),
                "DrawInternal",
                [
                    typeof(PlayerChoiceContext),
                    typeof(decimal),
                    typeof(Player),
                    typeof(bool),
                ]
            );

        if (drawInternal == null)
        {
            throw new MissingMethodException(
                "CardPileCmd.DrawInternal was not found."
            );
        }

        Harmony harmony = new(HarmonyId);
        harmony.Patch(
            populateCombatState,
            postfix: new HarmonyMethod(
                typeof(GuCardPileCombatPatch),
                nameof(PopulateCombatStatePostfix)
            )
        );

        harmony.Patch(
            drawInternal,
            prefix: new HarmonyMethod(
                typeof(GuCardPileCombatPatch),
                nameof(DrawInternalPrefix)
            )
        );

        _initialized = true;
    }

    internal static void Uninitialize()
    {
        try
        {
            new Harmony(HarmonyId).UnpatchAll(HarmonyId);
        }
        finally
        {
            _initialized = false;
        }
    }

    private static void PopulateCombatStatePostfix(
        Player __instance
    )
    {
        GuCardPileSystem.InitializeGuCardsForCombat(__instance);
    }

    private static void DrawInternalPrefix(Player player)
    {
        GuCardPileSystem.MoveStrayGuCardsToVillage(player);
    }
}
