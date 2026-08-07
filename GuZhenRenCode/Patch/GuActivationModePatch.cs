using System.Reflection;

using Godot;

using GuZhenRen.Cards;

using HarmonyLib;

using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Rooms;

using STS2RitsuLib.CardPiles.Nodes;

namespace GuZhenRen.Patches;

/// <summary>
/// 支撑蛊手牌（RitsuLib ExtraHand）的直接打出：允许蛊牌在资源可支付
/// 时开始原生 NCardPlay/目标选择流程，并保持蛊手牌布局与战斗结束/
/// 退出时的 pending 清理。
/// </summary>
internal static class GuActivationModePatch
{
    private const string HarmonyId =
        Entry.ModId + ".GuActivationMode";

    private static bool _initialized;

    internal static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        MethodInfo canStartExtraHandPlay =
            AccessTools.DeclaredMethod(
                typeof(NModExtraHand),
                "CanStartCardPlay",
                [typeof(NHandCardHolder)]
            ) ?? throw new MissingMethodException(
                typeof(NModExtraHand).FullName,
                "CanStartCardPlay(NHandCardHolder)"
            );

        MethodInfo extraHandProcess =
            AccessTools.DeclaredMethod(
                typeof(NModExtraHand),
                nameof(NModExtraHand._Process),
                [typeof(double)]
            ) ?? throw new MissingMethodException(
                typeof(NModExtraHand).FullName,
                "_Process(double)"
            );

        MethodInfo onCombatEnded =
            AccessTools.DeclaredMethod(
                typeof(NPlayerHand),
                "OnCombatEnded",
                [typeof(CombatRoom)]
            ) ?? throw new MissingMethodException(
                typeof(NPlayerHand).FullName,
                "OnCombatEnded(CombatRoom)"
            );

        MethodInfo exitTree =
            AccessTools.DeclaredMethod(
                typeof(NPlayerHand),
                nameof(NPlayerHand._ExitTree)
            ) ?? throw new MissingMethodException(
                typeof(NPlayerHand).FullName,
                "_ExitTree()"
            );

        Harmony harmony = new(HarmonyId);

        try
        {
            harmony.Patch(
                canStartExtraHandPlay,
                postfix: new HarmonyMethod(
                    typeof(GuActivationModePatch),
                    nameof(CanStartExtraHandPlayPostfix)
                )
            );

            harmony.Patch(
                extraHandProcess,
                postfix: new HarmonyMethod(
                    typeof(GuActivationModePatch),
                    nameof(ExtraHandProcessPostfix)
                )
            );

            harmony.Patch(
                onCombatEnded,
                prefix: new HarmonyMethod(
                    typeof(GuActivationModePatch),
                    nameof(CombatEndedPrefix)
                )
            );

            harmony.Patch(
                exitTree,
                prefix: new HarmonyMethod(
                    typeof(GuActivationModePatch),
                    nameof(ExitTreePrefix)
                )
            );

            _initialized = true;
        }
        catch
        {
            harmony.UnpatchAll(HarmonyId);
            GuActivationModeSystem.ResetWithoutUi();
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
            GuActivationModeSystem.ResetWithoutUi();
            _initialized = false;
        }
    }

    private static void CanStartExtraHandPlayPostfix(
        NModExtraHand __instance,
        NHandCardHolder holder,
        ref bool __result
    )
    {
        if (__instance.Definition.PileType !=
            GuCardPileSystem.PileType)
        {
            return;
        }

        // ExtraHand 的原生流程会在网络动作真正执行前，先在发起端本地
        // 把卡牌移入 Hand。上一张牌仍在执行时开始下一次选择，会让该
        // 本地临时状态被上一动作的 checksum 捕获，而其他端仍在蛊牌堆。
        if (GuCardPlaySyncPatch.IsCardActionExecuting)
        {
            __result = false;
            return;
        }

        __result = __result &&
            GuActivationModeSystem.CanSelect(holder.CardModel);

        if (__result && holder.CardModel != null)
        {
            GuActivationModeSystem.MarkGuCardSelected(
                holder.CardModel
            );
        }
    }

    private static void ExtraHandProcessPostfix(
        NModExtraHand __instance
    )
    {
        GuActivationModeSystem.UpdateExtraHandLayout(__instance);
    }

    private static void CombatEndedPrefix()
    {
        GuActivationModeSystem.Cancel("战斗已经结束。");

        /*
         * 杀招材料封装标记挂在战斗卡牌实例的 SavedAttachedState 上，
         * 战斗结束所有战斗实例销毁后标记随之消失，永久牌组中的蛊
         * 不受影响，因此这里无需显式遍历清理。
         */
    }

    private static void ExitTreePrefix()
    {
        GuActivationModeSystem.ResetWithoutUi();
    }
}
