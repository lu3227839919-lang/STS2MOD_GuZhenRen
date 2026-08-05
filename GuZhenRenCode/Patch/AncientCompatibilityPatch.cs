using System.Reflection;

using GuZhenRen.Characters;

using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;

using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;

namespace GuZhenRen.Patches;

/// <summary>
/// 为自定义角色补齐先古遗民与遗物初始化的兼容保护。
///
/// BaseLib 的 ITomeCard 兼容补丁会扩展 DustyTome.SetupForPlayer。
/// 当角色没有 BaseLib 预期的原版卡池属性时，该补丁可能在原版初始化
/// 已经完成之后抛出 NullReferenceException，进而阻断 DARV 事件。
///
/// 本补丁只对蛊真人角色、只对 DustyTome 的 NullReferenceException 生效；
/// 其他角色、其他异常仍按原样抛出，避免隐藏无关故障。
/// </summary>
internal static class AncientCompatibilityPatch
{
    private const string HarmonyId =
        Entry.ModId + ".AncientCompatibility";

    private static bool _initialized;

    internal static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        MethodInfo? setupForPlayer =
            AccessTools.GetDeclaredMethods(typeof(DustyTome))
                .FirstOrDefault(static method =>
                {
                    if (!string.Equals(
                            method.Name,
                            "SetupForPlayer",
                            StringComparison.Ordinal
                        ))
                    {
                        return false;
                    }

                    ParameterInfo[] parameters = method.GetParameters();
                    return parameters.Length == 1 &&
                           parameters[0].ParameterType == typeof(Player);
                });

        if (setupForPlayer == null)
        {
            Entry.Logger.Warn(
                "[先古遗民兼容] 未找到 DustyTome.SetupForPlayer(Player)，" +
                "已跳过该可选保护。"
            );
            _initialized = true;
            return;
        }

        HarmonyMethod finalizer = new(
            typeof(AncientCompatibilityPatch),
            nameof(DustyTomeSetupFinalizer)
        )
        {
            priority = Priority.Last,
        };

        new Harmony(HarmonyId).Patch(
            setupForPlayer,
            finalizer: finalizer
        );

        _initialized = true;
        Entry.Logger.Info(
            "[先古遗民兼容] 已安装 DustyTome 空引用保护；" +
            "DARV 与其他先古遗民文本已覆盖蛊真人角色。"
        );
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

    private static Exception? DustyTomeSetupFinalizer(
        DustyTome __instance,
        Player? __0,
        Exception? __exception
    )
    {
        if (__exception == null)
        {
            return null;
        }

        if (__0?.Character is not GuZhenRenCharacter)
        {
            return __exception;
        }

        if (__exception is not NullReferenceException)
        {
            return __exception;
        }

        Entry.Logger.Warn(
            "[先古遗民兼容] DustyTome.SetupForPlayer 在蛊真人角色上" +
            "触发空引用。原版遗物初始化已保留，已忽略第三方扩展异常，" +
            "避免 DARV 事件被中断。" +
            $" 错误：{__exception.Message}"
        );

        return null;
    }
}
