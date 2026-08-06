using System.Reflection;

using Godot;

using GuZhenRen.Cards;

using HarmonyLib;

using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Rooms;

using STS2RitsuLib.CardPiles.Nodes;

namespace GuZhenRen.Patches;

/// <summary>
/// 限制 RitsuLib ExtraHand 中的蛊牌：只有普通手牌中存在可用“催动”
/// 时才允许开始原生 NCardPlay/目标选择流程。旧催动模式的 UI 清理
/// 补丁保留为兼容入口，但正常流程不再禁用普通手牌。
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

        MethodInfo canPlayNormalHand =
            AccessTools.DeclaredMethod(
                typeof(NPlayerHand),
                "CanPlayCards"
            ) ?? throw new MissingMethodException(
                typeof(NPlayerHand).FullName,
                "CanPlayCards()"
            );

        MethodInfo areCardActionsAllowed =
            AccessTools.DeclaredMethod(
                typeof(NPlayerHand),
                "AreCardActionsAllowed"
            ) ?? throw new MissingMethodException(
                typeof(NPlayerHand).FullName,
                "AreCardActionsAllowed()"
            );

        MethodInfo animEnable =
            AccessTools.DeclaredMethod(
                typeof(NPlayerHand),
                "AnimEnable"
            ) ?? throw new MissingMethodException(
                typeof(NPlayerHand).FullName,
                "AnimEnable()"
            );

        MethodInfo onPlayerActionsDisabledChanged =
            AccessTools.DeclaredMethod(
                typeof(NPlayerHand),
                "OnPlayerActionsDisabledChanged",
                [typeof(CombatState)]
            ) ?? throw new MissingMethodException(
                typeof(NPlayerHand).FullName,
                "OnPlayerActionsDisabledChanged(CombatState)"
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

        MethodInfo unhandledInput =
            AccessTools.DeclaredMethod(
                typeof(NPlayerHand),
                nameof(NPlayerHand._UnhandledInput),
                [typeof(InputEvent)]
            ) ?? throw new MissingMethodException(
                typeof(NPlayerHand).FullName,
                "_UnhandledInput(InputEvent)"
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
                canPlayNormalHand,
                postfix: new HarmonyMethod(
                    typeof(GuActivationModePatch),
                    nameof(CanPlayNormalHandPostfix)
                )
            );

            harmony.Patch(
                areCardActionsAllowed,
                postfix: new HarmonyMethod(
                    typeof(GuActivationModePatch),
                    nameof(CanPlayNormalHandPostfix)
                )
            );

            harmony.Patch(
                animEnable,
                prefix: new HarmonyMethod(
                    typeof(GuActivationModePatch),
                    nameof(AnimEnablePrefix)
                )
            );

            harmony.Patch(
                onPlayerActionsDisabledChanged,
                prefix: new HarmonyMethod(
                    typeof(GuActivationModePatch),
                    nameof(PlayerActionsDisabledChangedPrefix)
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
                unhandledInput,
                prefix: new HarmonyMethod(
                    typeof(GuActivationModePatch),
                    nameof(UnhandledInputPrefix)
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

        __result = __result &&
            GuActivationModeSystem.CanSelect(holder.CardModel);

        if (__result && holder.CardModel != null)
        {
            GuActivationModeSystem.PrepareTargeting(
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

    private static void CanPlayNormalHandPostfix(
        ref bool __result
    )
    {
        if (GuActivationModeSystem.ShouldBlockNormalHand())
        {
            __result = false;
        }
    }

    private static bool AnimEnablePrefix() =>
        !GuActivationModeSystem.ShouldBlockNormalHand();

    private static void PlayerActionsDisabledChangedPrefix()
    {
        GuActivationModeSystem.CancelIfPlayerActionsDisabled();
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

    private static bool UnhandledInputPrefix(
        InputEvent input
    )
    {
        if (!GuActivationModeSystem.IsActive ||
            NTargetManager.Instance.IsInSelection)
        {
            return true;
        }

        if (!input.IsActionPressed(MegaInput.cancel) &&
            !input.IsActionPressed(MegaInput.pauseAndBack))
        {
            return true;
        }

        GuActivationModeSystem.Cancel("玩家返回普通手牌。");
        NPlayerHand.Instance?.GetViewport()?.SetInputAsHandled();
        return false;
    }

    private static void ExitTreePrefix()
    {
        GuActivationModeSystem.ResetWithoutUi();
    }
}
