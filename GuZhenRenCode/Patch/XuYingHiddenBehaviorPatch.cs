using System.Reflection;

using GuZhenRen.Cards;

using HarmonyLib;

using MegaCrit.Sts2.Core.Models;

namespace GuZhenRen.Patches;

/// <summary>
/// 保留虚影的玩法规则，同时不向卡面公开 Retain 关键词。
///
/// CardModel.ShouldRetainThisTurn 是原版回合结束清理手牌时使用的
/// 最终判定入口。对虚影返回 true，可以保持原有玩法，而不会让
/// “保留”出现在关键词、悬停说明或卡面标签中。
/// </summary>
internal static class XuYingHiddenBehaviorPatch
{
    private const string HarmonyId =
        Entry.ModId + ".XuYingHiddenBehavior";

    private static bool _initialized;

    internal static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        MethodInfo original =
            AccessTools.PropertyGetter(
                typeof(CardModel),
                nameof(CardModel.ShouldRetainThisTurn)
            )
            ?? throw new MissingMethodException(
                typeof(CardModel).FullName,
                "get_" +
                nameof(CardModel.ShouldRetainThisTurn)
            );

        MethodInfo postfix =
            AccessTools.Method(
                typeof(XuYingHiddenBehaviorPatch),
                nameof(ShouldRetainThisTurnPostfix)
            )
            ?? throw new MissingMethodException(
                typeof(XuYingHiddenBehaviorPatch).FullName,
                nameof(ShouldRetainThisTurnPostfix)
            );

        new Harmony(HarmonyId).Patch(
            original,
            postfix: new HarmonyMethod(postfix)
        );

        _initialized = true;
    }

    internal static void Uninitialize()
    {
        try
        {
            new Harmony(HarmonyId)
                .UnpatchAll(HarmonyId);
        }
        finally
        {
            _initialized = false;
        }
    }

    private static void ShouldRetainThisTurnPostfix(
        CardModel __instance,
        ref bool __result
    )
    {
        if (__instance is AbstractXuYingCard)
        {
            __result = true;
        }
    }
}
