using GuZhenRen.Powers.GuangDao;

using MegaCrit.Sts2.Core.Nodes.Combat;

using STS2RitsuLib;
using STS2RitsuLib.Patching.Core;
using STS2RitsuLib.Patching.Models;

namespace GuZhenRen.Patches;

/// <summary>
/// 隐藏折光的战斗 Power 节点。
/// 折光模型仍保留在战斗状态中，因此触发、计数、存档与多人同步均不受影响。
/// </summary>
internal static class ZheGuangPowerVisibilityPatch
{
    private const string PatcherName =
        "ZheGuangPowerVisibility";

    private static ModPatcher? _patcher;

    internal static void Initialize()
    {
        if (_patcher is not null)
        {
            return;
        }

        ModPatcher patcher = RitsuLibFramework.CreatePatcher(
            Entry.ModId,
            PatcherName,
            "hide the Zhe Guang power icon"
        );

        patcher.RegisterPatch<HideZheGuangPowerNodePatch>();

        try
        {
            patcher.PatchAll();
            _patcher = patcher;
        }
        catch
        {
            patcher.UnpatchAll();
            throw;
        }
    }

    internal static void Uninitialize()
    {
        try
        {
            _patcher?.UnpatchAll();
        }
        finally
        {
            _patcher = null;
        }
    }

    private sealed class HideZheGuangPowerNodePatch : IPatchMethod
    {
        public static string PatchId =>
            "hide_zhe_guang_power_node";

        public static bool IsCritical => false;

        public static string Description =>
            "Hide the display-only node for Zhe Guang";

        public static ModPatchTarget[] GetTargets()
        {
            return [new(typeof(NPower), nameof(NPower._Ready))];
        }

        public static void Postfix(NPower __instance)
        {
            if (__instance.Model is ZheGuangPower)
            {
                __instance.Visible = false;
            }
        }
    }
}
