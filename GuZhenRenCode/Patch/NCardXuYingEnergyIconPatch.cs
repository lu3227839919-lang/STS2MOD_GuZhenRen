using System.Reflection;

using Godot;
using HarmonyLib;

using GuZhenRen.Cards;

using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace GuZhenRen.Patches;

/// <summary>
/// Hides the normal energy icon for visible Phantom cards.
/// Hidden/unseen cards keep the vanilla icon so card identity is not leaked.
/// </summary>
internal static class NCardXuYingEnergyIconPatch
{
    private const string HarmonyId = Entry.ModId + ".NCardXuYingEnergyIcon";

    private static FieldInfo? _energyIconField;
    private static bool _initialized;

    internal static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        MethodInfo original = AccessTools.DeclaredMethod(
            typeof(NCard),
            "UpdateEnergyCostVisuals",
            [typeof(PileType)]
        ) ?? throw new MissingMethodException(
            typeof(NCard).FullName,
            "UpdateEnergyCostVisuals(PileType)"
        );

        _energyIconField = AccessTools.Field(
            typeof(NCard),
            "_energyIcon"
        ) ?? throw new MissingFieldException(
            typeof(NCard).FullName,
            "_energyIcon"
        );

        Harmony harmony = new(HarmonyId);
        try
        {
            harmony.Patch(
                original,
                postfix: new HarmonyMethod(
                    typeof(NCardXuYingEnergyIconPatch),
                    nameof(Postfix)
                )
            );
            _initialized = true;
        }
        catch
        {
            harmony.UnpatchAll(HarmonyId);
            _energyIconField = null;
            throw;
        }
    }

    internal static void Uninitialize()
    {
        try
        {
            new Harmony(HarmonyId).UnpatchAll(HarmonyId);
        }
        finally
        {
            _energyIconField = null;
            _initialized = false;
        }
    }

    private static void Postfix(NCard __instance)
    {
        if (__instance.Visibility != ModelVisibility.Visible ||
            __instance.Model is not AbstractXuYingCard)
        {
            return;
        }

        if (_energyIconField?.GetValue(__instance) is TextureRect energyIcon)
        {
            energyIcon.Visible = false;
        }
    }
}
