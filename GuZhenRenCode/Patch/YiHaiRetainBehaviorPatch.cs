using System.Reflection;

using GuZhenRen.Cards.XueDao;

using HarmonyLib;

using MegaCrit.Sts2.Core.Models;

namespace GuZhenRen.Patches;

/// <summary>
/// 保证遗骸（YiHai）跨回合保留在手牌。
///
/// 遗骸在 CanonicalKeywords 中声明了 CardKeyword.Retain，原版回合结束
/// 清理手牌（CombatManager.FlushPlayerHand）会依据
/// CardModel.ShouldRetainThisTurn 决定去留。但部分路径（多人同步/
/// 序列化）可能先把实例 Keywords 物化为空集，导致 CanonicalKeywords
/// 不再并入、Retain 失效。这里在原版最终判定入口对遗骸直接返回 true，
/// 不依赖关键词集合是否物化，与虚影保留补丁（XuYingHiddenBehaviorPatch）
/// 使用同一模式。
/// </summary>
internal static class YiHaiRetainBehaviorPatch
{
    private const string HarmonyId =
        Entry.ModId + ".YiHaiRetainBehavior";

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
                typeof(YiHaiRetainBehaviorPatch),
                nameof(ShouldRetainThisTurnPostfix)
            )
            ?? throw new MissingMethodException(
                typeof(YiHaiRetainBehaviorPatch).FullName,
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
        if (__instance is YiHai)
        {
            __result = true;
        }
    }
}
