using System.Reflection;

using GuZhenRen.Powers.LiDao;

using HarmonyLib;

using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace GuZhenRen.Patches;

/// <summary>
/// 伤力回天的一次性触发器。
///
/// ShouldDie：只有原版及其他效果都没有阻止死亡时，回天才抢占本次
/// 死亡，并把自己登记为 preventer。
///
/// AfterPreventingDeath：如果 preventer 正是伤力回天，则在死亡
/// 被阻止后立刻按记录的最高后续用牌伤害回复生命。
///
/// AfterCombatEnd：若本场从未濒死触发，则在战斗牌堆清空之前兜底
/// 结算一次回复。Power 自身会在回复后移除，保证不会重复。
/// </summary>
internal static class ShangLiHuiTianTriggerPatch
{
    private const string HarmonyId =
        Entry.ModId + ".ShangLiHuiTianTrigger";

    private static bool _initialized;

    internal static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        Harmony harmony = new(HarmonyId);

        harmony.Patch(
            AccessTools.Method(
                typeof(Hook),
                nameof(Hook.ShouldDie)
            ) ?? throw new MissingMethodException(
                typeof(Hook).FullName,
                nameof(Hook.ShouldDie)
            ),
            postfix: new HarmonyMethod(
                typeof(ShangLiHuiTianTriggerPatch),
                nameof(ShouldDiePostfix)
            )
        );

        harmony.Patch(
            AccessTools.Method(
                typeof(Hook),
                nameof(Hook.AfterPreventingDeath)
            ) ?? throw new MissingMethodException(
                typeof(Hook).FullName,
                nameof(Hook.AfterPreventingDeath)
            ),
            postfix: new HarmonyMethod(
                typeof(ShangLiHuiTianTriggerPatch),
                nameof(AfterPreventingDeathPostfix)
            )
        );

        harmony.Patch(
            AccessTools.Method(
                typeof(Hook),
                nameof(Hook.AfterCombatEnd)
            ) ?? throw new MissingMethodException(
                typeof(Hook).FullName,
                nameof(Hook.AfterCombatEnd)
            ),
            postfix: new HarmonyMethod(
                typeof(ShangLiHuiTianTriggerPatch),
                nameof(AfterCombatEndPostfix)
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

    private static void ShouldDiePostfix(
        Creature creature,
        ref bool __result,
        ref AbstractModel? preventer
    )
    {
        // 其他模型已经阻止死亡时不抢优先级，回天留待下一次濒死。
        if (!__result)
        {
            return;
        }

        ShangLiHuiTianPower? power =
            creature.GetPower<ShangLiHuiTianPower>();
        if (power == null || !power.TryClaimDeathPrevention())
        {
            return;
        }

        __result = false;
        preventer = power;

        Entry.Logger.Info(
            $"[伤力回天] 抢占濒死判定 creature={creature.CombatId} " +
            $"stored={power.MaxRecordedDamage}。"
        );
    }

    private static void AfterPreventingDeathPostfix(
        ref Task __result,
        AbstractModel preventer,
        Creature creature
    )
    {
        if (preventer is not ShangLiHuiTianPower power ||
            !ReferenceEquals(power.Owner, creature))
        {
            return;
        }

        __result = AwaitDeathRecoveryAsync(__result, power);
    }

    private static async Task AwaitDeathRecoveryAsync(
        Task original,
        ShangLiHuiTianPower power
    )
    {
        await original;
        await power.TriggerRecoveryAsync("near-death");
    }

    private static void AfterCombatEndPostfix(
        ref Task __result,
        IRunState runState,
        CombatRoom room
    )
    {
        __result = AwaitCombatEndRecoveryAsync(__result, runState);
    }

    private static async Task AwaitCombatEndRecoveryAsync(
        Task original,
        IRunState runState
    )
    {
        await original;

        foreach (Player player in runState.Players)
        {
            ShangLiHuiTianPower? power =
                player.Creature.GetPower<ShangLiHuiTianPower>();
            if (power == null)
            {
                continue;
            }

            await power.TriggerRecoveryAsync("combat-end");
        }
    }
}
