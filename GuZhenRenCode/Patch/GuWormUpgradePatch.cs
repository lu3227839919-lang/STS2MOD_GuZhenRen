using System.Reflection;

using HarmonyLib;

using GuZhenRen.Cards;

using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRen.Patches;

/// <summary>
/// 蛊虫牌完全退出游戏原生卡牌升级入口。
///
/// 锻造与其他升级事件只允许选择普通牌；蛊虫的所有转数成长均由升炼处理。
/// 六转及以上蛊虫只在状态语义上视为已升级，不执行原生升级生命周期，
/// 因而不会恢复任何已删除的伤害、格挡、催动次数或衍生牌升级效果。
/// </summary>
internal static class GuWormUpgradePatch
{
    private const string HarmonyId =
        Entry.ModId + ".GuWormUpgrade";

    private static bool _initialized;

    internal static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        MethodInfo? isUpgradableGetter =
            AccessTools.PropertyGetter(
                typeof(CardModel),
                nameof(CardModel.IsUpgradable)
            );

        if (isUpgradableGetter == null)
        {
            throw new MissingMethodException(
                "蛊虫升级屏蔽所需的 CardModel.IsUpgradable 不存在。"
            );
        }

        MethodInfo? isUpgradedGetter =
            AccessTools.PropertyGetter(
                typeof(CardModel),
                nameof(CardModel.IsUpgraded)
            );

        if (isUpgradedGetter == null)
        {
            throw new MissingMethodException(
                "仙蛊升级状态映射所需的 CardModel.IsUpgraded 不存在。"
            );
        }

        Harmony harmony = new(HarmonyId);
        harmony.Patch(
            isUpgradableGetter,
            postfix: new HarmonyMethod(
                typeof(GuWormUpgradePatch),
                nameof(IsUpgradablePostfix)
            )
        );
        harmony.Patch(
            isUpgradedGetter,
            postfix: new HarmonyMethod(
                typeof(GuWormUpgradePatch),
                nameof(IsUpgradedPostfix)
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

    private static void IsUpgradablePostfix(
        CardModel __instance,
        ref bool __result
    )
    {
        if (__instance is AbstractGuWormCard)
        {
            // 无论转数或显示出的升级状态如何，蛊牌都不能成为
            // 锻造、升级事件或其他原生升级选牌界面的候选。
            __result = false;
        }
    }

    private static void IsUpgradedPostfix(
        CardModel __instance,
        ref bool __result
    )
    {
        if (__instance is AbstractGuWormCard gu)
        {
            // 仙蛊的“已升级”状态完全由永久基础转数派生。
            // 强制覆盖旧存档可能残留的原生升级标记：
            // 一至五转始终为未升级，六至九转始终为已升级。
            __result =
                gu.BaseGuRank >=
                GuZhenRenCardRules.XianGuRank;
        }
    }
}
