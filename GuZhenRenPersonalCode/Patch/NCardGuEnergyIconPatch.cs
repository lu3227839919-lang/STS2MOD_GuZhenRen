using System.Reflection;

using Godot;
using HarmonyLib;

using GuZhenRen.Cards;
using GuZhenRen.Combat;

using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Cards;

using STS2RitsuLib.Combat.SecondaryResources;

namespace GuZhenRen.Patches;

/// <summary>
/// 让蛊虫牌的费用区域显示元气费用，并用淡蓝灰色与原生能量区分。
/// 该补丁只修改本地 NCard 节点，不改变战斗状态或多人同步数据。
/// </summary>
internal static class NCardGuEnergyIconPatch
{
    private const string HarmonyId =
        Entry.ModId + ".NCardGuEnergyIcon";

    private const string AppliedMeta =
        "GuZhenRenGuEnergyIconApplied";

    private static readonly Color IconColor =
        new(0.68f, 0.80f, 0.86f, 1f);

    private static readonly Color TextColor =
        new(0.80f, 0.90f, 0.95f, 1f);

    private static readonly Color OutlineColor =
        new(0.08f, 0.18f, 0.24f, 1f);

    private static FieldInfo? _energyIconField;
    private static FieldInfo? _energyLabelField;
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

        _energyLabelField = AccessTools.Field(
            typeof(NCard),
            "_energyLabel"
        ) ?? throw new MissingFieldException(
            typeof(NCard).FullName,
            "_energyLabel"
        );

        Harmony harmony = new(HarmonyId);

        try
        {
            harmony.Patch(
                original,
                postfix: new HarmonyMethod(
                    typeof(NCardGuEnergyIconPatch),
                    nameof(Postfix)
                )
            );
            _initialized = true;
        }
        catch
        {
            harmony.UnpatchAll(HarmonyId);
            ResetReflectionState();
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
            ResetReflectionState();
            _initialized = false;
        }
    }

    private static void Postfix(NCard __instance)
    {
        if (_energyIconField?.GetValue(__instance) is not
                TextureRect energyIcon ||
            _energyLabelField?.GetValue(__instance) is not
                MegaLabel energyLabel)
        {
            return;
        }

        if (__instance.Visibility != ModelVisibility.Visible ||
            __instance.Model is not IGuWormCard)
        {
            if (energyIcon.HasMeta(AppliedMeta))
            {
                energyIcon.SelfModulate = Colors.White;
                energyIcon.RemoveMeta(AppliedMeta);
            }

            return;
        }

        SecondaryResourcePaymentLine? yuanQiLine =
            GuCardUsageRules
                .CreateActivationPaymentPlan(__instance.Model)
                .Lines
                .FirstOrDefault(line => string.Equals(
                    line.ResourceId,
                    YuanQiSystem.ResourceId,
                    StringComparison.OrdinalIgnoreCase
                ));

        string amountText = yuanQiLine switch
        {
            { CostsX: true } => "X",
            { } => yuanQiLine.Cost.ToString(),
            _ => "0",
        };

        energyLabel.SetTextAutoSize(amountText);
        energyLabel.AddThemeColorOverride(
            ThemeConstants.Label.FontColor,
            TextColor
        );
        energyLabel.AddThemeColorOverride(
            ThemeConstants.Label.FontOutlineColor,
            OutlineColor
        );

        energyIcon.Texture = __instance.Model.EnergyIcon;
        energyIcon.SelfModulate = IconColor;
        energyIcon.Visible = true;
        energyIcon.SetMeta(AppliedMeta, true);
    }

    private static void ResetReflectionState()
    {
        _energyIconField = null;
        _energyLabelField = null;
    }
}
