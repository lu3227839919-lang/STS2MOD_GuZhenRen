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
                .Where(IsCompatibleModifyHpLost)
                .ToArray();

        if (candidates.Length != 1)
        {
            throw new InvalidOperationException(
                $"期望唯一匹配 {HookTypeName}.{ModifyHpLostMethodName}，" +
                $"实际匹配 {candidates.Length} 个。"
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

            harmony.Patch(
                candidates[0],
                postfix: postfix
            );

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

    private static bool IsCompatibleModifyHpLost(
        MethodInfo method
    )
    {
        ParameterInfo[] parameters =
            method.GetParameters();

        return
            method.Name == ModifyHpLostMethodName &&
            method.ReturnType == typeof(decimal) &&
            parameters.Length == 9 &&
            parameters[2].ParameterType == typeof(Creature) &&
            parameters[7].ParameterType.IsEnum &&
            parameters[7].ParameterType.Name == "HpLossHookPhase" &&
            parameters[8].IsOut;
    }

    /// <summary>
    /// 只绑定实际需要的入参。这里不能使用 Harmony 的 __args：
    /// ModifyHpLost 含有 out modifiers，而 __args 在原方法执行前建立快照；
    /// Postfix 结束时 Harmony 会把快照写回，导致原方法生成的 modifiers
    /// 被旧的 null 覆盖。
    /// </summary>
    private static void Postfix(
        [HarmonyArgument(2)] Creature target,
        [HarmonyArgument(7)] object phase,
        ref decimal __result
    )
    {
        if (__result <= 0m ||
            !string.Equals(
                phase.ToString(),
                "AfterOsty",
                StringComparison.Ordinal
            ))
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

}
