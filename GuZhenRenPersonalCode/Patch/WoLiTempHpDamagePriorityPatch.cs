using System.Reflection;

using HarmonyLib;

using GuZhenRen.Powers.LiDao;

using MegaCrit.Sts2.Core.Entities.Creatures;

namespace GuZhenRen.Patches;

/// <summary>
/// 万我临时生命承伤优先级：
/// 格挡/其它前置防护 → 万我临时生命 → 角色真实生命。
///
/// STS2 0.106+ 的最终 HP 损失统一经过 Hook.ModifyHpLost，
/// 并通过 HpLossHookPhase 区分 BeforeOsty / AfterOsty。
/// 本补丁仅在 AfterOsty 阶段的 Hook 返回之后修改最终 HP loss，
/// 因此不会抢在格挡之前吸收，也不会修改敌方攻击 Intent。
/// </summary>
internal static class WoLiTempHpDamagePriorityPatch
{
    private const string HarmonyId =
        Entry.ModId + ".WoLiTempHpDamagePriority";

    private const string HookTypeName =
        "MegaCrit.Sts2.Core.Hooks.Hook";

    private const string ModifyHpLostMethodName =
        "ModifyHpLost";

    private static bool _initialized;

    internal static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        Type hookType =
            AccessTools.TypeByName(HookTypeName) ??
            throw new TypeLoadException(
                $"无法找到 {HookTypeName}。"
            );

        MethodInfo[] candidates =
            hookType.GetMethods(
                    BindingFlags.Static |
                    BindingFlags.Public |
                    BindingFlags.NonPublic
                )
                .Where(method =>
                    method.Name == ModifyHpLostMethodName &&
                    method.ReturnType == typeof(decimal))
                .ToArray();

        if (candidates.Length == 0)
        {
            throw new MissingMethodException(
                HookTypeName,
                ModifyHpLostMethodName
            );
        }

        Harmony harmony = new(HarmonyId);

        try
        {
            HarmonyMethod postfix = new(
                typeof(WoLiTempHpDamagePriorityPatch),
                nameof(Postfix)
            )
            {
                priority = Priority.Last,
            };

            foreach (MethodInfo method in candidates)
            {
                harmony.Patch(
                    method,
                    postfix: postfix
                );
            }

            _initialized = true;
        }
        catch
        {
            harmony.UnpatchAll(HarmonyId);
            throw;
        }
    }

    internal static void Uninitialize()
    {
        new Harmony(HarmonyId).UnpatchAll(HarmonyId);
        _initialized = false;
    }

    private static void Postfix(
        MethodBase __originalMethod,
        object[] __args,
        ref decimal __result
    )
    {
        if (__result <= 0m ||
            !IsAfterOstyPhase(__args))
        {
            return;
        }

        Creature? target =
            FindTarget(
                __originalMethod,
                __args
            );

        if (target is null)
        {
            return;
        }

        WoLiTempHpPower? tempHpPower =
            target.GetPower<WoLiTempHpPower>();

        if (tempHpPower is null ||
            tempHpPower.TempHp <= 0)
        {
            return;
        }

        int absorbed =
            tempHpPower.PrepareFinalHpLossAbsorption(
                __result
            );

        if (absorbed <= 0)
        {
            return;
        }

        __result = Math.Max(
            0m,
            __result - absorbed
        );
    }

    private static bool IsAfterOstyPhase(
        object[] args
    )
    {
        foreach (object? argument in args)
        {
            if (argument is not Enum phase ||
                phase.GetType().Name !=
                    "HpLossHookPhase")
            {
                continue;
            }

            string phaseName =
                phase.ToString();

            return phaseName.Contains(
                "AfterOsty",
                StringComparison.Ordinal
            );
        }

        return false;
    }

    private static Creature? FindTarget(
        MethodBase originalMethod,
        object[] args
    )
    {
        ParameterInfo[] parameters =
            originalMethod.GetParameters();

        int count = Math.Min(
            parameters.Length,
            args.Length
        );

        for (int index = 0;
             index < count;
             index++)
        {
            if (string.Equals(
                    parameters[index].Name,
                    "target",
                    StringComparison.OrdinalIgnoreCase
                ) &&
                args[index] is Creature target)
            {
                return target;
            }
        }

        foreach (object? argument in args)
        {
            if (argument is Creature creature)
            {
                return creature;
            }
        }

        return null;
    }
}
