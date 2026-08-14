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

        Type extraHandPlayCoordinator =
            typeof(NModExtraHand).Assembly.GetType(
                "STS2RitsuLib.CardPiles.ModExtraHandPlayCoordinator",
                throwOnError: true
            )!;

        MethodInfo tryBeginExtraHandPlay =
            AccessTools.DeclaredMethod(
                extraHandPlayCoordinator,
                "TryBegin",
                [typeof(NModExtraHand), typeof(NHandCardHolder)]
            ) ?? throw new MissingMethodException(
                extraHandPlayCoordinator.FullName,
                "TryBegin(NModExtraHand, NHandCardHolder)"
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
                tryBeginExtraHandPlay,
                postfix: new HarmonyMethod(
                    typeof(GuActivationModePatch),
                    nameof(ExtraHandPlayBeginPostfix)
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
        // 把卡牌移入 Hand。仅当这张蛊牌所属玩家自己的上一张牌仍在执行
        // 时禁止下一次选择；队友的出牌（尤其等待弃牌选择的长动作）不能
        // 锁住方源的蛊手牌。跨玩家并发动作的 checksum 差异由
        // NormalizePendingCardForChecksum 负责归一化。
        if (holder.CardModel != null &&
            GuCardPlaySyncPatch.IsCardActionExecuting(
                holder.CardModel.Owner
            ))
        {
            __result = false;
            return;
        }

        __result = __result &&
            GuActivationModeSystem.CanSelect(holder.CardModel);
    }

    /// <summary>
    /// CanStartCardPlay 只是资格检查，RitsuLib 随后的 TryBegin 仍可能因
    /// 牌堆在同一交互帧发生变化而拒绝开始。只有 TryBegin 返回成功时，
    /// 卡牌才已从蛊手牌原子移入 Hand；此时登记 pending 才不会留下
    /// “资格检查通过但事务未开始”的幽灵选择状态。
    /// </summary>
    private static void ExtraHandPlayBeginPostfix(
        NModExtraHand container,
        NHandCardHolder holder,
        bool __result
    )
    {
        if (!__result ||
            container.Definition.PileType !=
                GuCardPileSystem.PileType ||
            holder.CardModel == null)
        {
            return;
        }

        GuActivationModeSystem.MarkGuCardSelected(holder.CardModel);
    }

    private static void ExtraHandProcessPostfix(
        NModExtraHand __instance
    )
    {
        GuActivationModeSystem.UpdateExtraHandLayout(__instance);

        // 玩家点击蛊牌后若取消/放弃目标选择，RitsuLib 会把牌移回蛊牌堆
        // 且不会通知本模组，_pendingCard 会残留并锁死其余蛊牌的选择。
        // 每帧兜底清理：pending 卡已回到蛊牌堆即视为选择已放弃。
        GuActivationModeSystem.SweepStalePending();
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
