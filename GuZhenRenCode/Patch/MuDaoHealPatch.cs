using System;
using System.Linq;
using System.Reflection;

using HarmonyLib;

using GuZhenRen.Powers;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace GuZhenRen.Patches;

/// <summary>
/// 让 CreatureCmd.Heal 支持模组治疗量修改能力。
///
/// 仅修改 amount 参数，不替代原版治疗、动画、音效和生命上限逻辑。
/// </summary>
internal static class MuDaoHealPatch
{
    private const string HarmonyId =
        Entry.ModId + ".MuDaoHeal";

    private static bool _initialized;

    /// <summary>
    /// 安装补丁。应在 Entry.Initialize() 中调用一次。
    /// </summary>
    internal static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        MethodInfo original =
            AccessTools.Method(
                typeof(CreatureCmd),
                nameof(CreatureCmd.Heal),
                [
                    typeof(Creature),
                    typeof(decimal),
                    typeof(bool)
                ]
            )
            ?? throw new MissingMethodException(
                typeof(CreatureCmd).FullName,
                nameof(CreatureCmd.Heal)
            );

        MethodInfo prefix =
            AccessTools.Method(
                typeof(MuDaoHealPatch),
                nameof(Prefix)
            )
            ?? throw new MissingMethodException(
                typeof(MuDaoHealPatch).FullName,
                nameof(Prefix)
            );

        Harmony harmony =
            new(HarmonyId);

        harmony.Patch(
            original,
            prefix:
                new HarmonyMethod(prefix)
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

    /// <summary>
    /// 在原版治疗执行前汇总受治疗者身上的治疗修改能力。
    /// </summary>
    private static void Prefix(
        Creature creature,
        ref decimal amount
    )
    {
        if (creature == null ||
            amount <= 0m)
        {
            return;
        }

        // ToArray 避免修改过程中能力列表变化导致枚举失效。
        IGuZhenRenHealAmountModifier[] modifiers =
            creature
                .Powers
                .OfType<IGuZhenRenHealAmountModifier>()
                .ToArray();

        foreach (
            IGuZhenRenHealAmountModifier modifier
            in modifiers
        )
        {
            amount =
                modifier.ModifyHealAmount(
                    creature,
                    amount
                );

            if (amount <= 0m)
            {
                amount = 0m;
                return;
            }
        }
    }
}
