using System.Reflection;

using HarmonyLib;

using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRen.Patches;

/// <summary>
/// STS2 v0.111.0 的 HP 损失收尾回调会直接枚举伤害结算返回的
/// modifiers。部分经框架转发的 AttackCommand 在没有修正器时会把
/// null 传入该回调，导致卡牌在实际扣血前抛出 ArgumentNullException。
///
/// null 在这里与“没有修正器”等价；统一为空集合后，原版回调会正常
/// 跳过通知，同时保留伤害、格挡、Osty 以及攻击命中钩子的原有流程。
/// </summary>
internal static class HpLossModifierNullGuardPatch
{
    private const string HarmonyId =
        Entry.ModId + ".HpLossModifierNullGuard";

    private static bool _initialized;

    internal static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        MethodInfo[] targets =
        [
            RequireHookMethod(nameof(Hook.AfterModifyingHpLostBeforeOsty)),
            RequireHookMethod(nameof(Hook.AfterModifyingHpLostAfterOsty)),
        ];

        Harmony harmony = new(HarmonyId);

        try
        {
            HarmonyMethod prefix = new(
                typeof(HpLossModifierNullGuardPatch),
                nameof(Prefix)
            )
            {
                priority = Priority.First,
            };

            foreach (MethodInfo target in targets)
            {
                harmony.Patch(target, prefix: prefix);
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

    private static void Prefix(
        MethodBase __originalMethod,
        ref IEnumerable<AbstractModel>? modifiers
    )
    {
        if (modifiers is not null)
        {
            return;
        }

        modifiers = Array.Empty<AbstractModel>();

        Entry.Logger.Warn(
            $"[HP 损失兼容] {__originalMethod.Name} 收到空 modifiers；" +
            "已按无修正器处理，继续卡牌结算。"
        );
    }

    private static MethodInfo RequireHookMethod(string name)
    {
        return AccessTools.Method(typeof(Hook), name) ??
            throw new MissingMethodException(typeof(Hook).FullName, name);
    }
}
