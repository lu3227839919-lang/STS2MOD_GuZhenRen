using System.Reflection;

using HarmonyLib;

using GuZhenRen.Cards;

using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRen.Patches;

/// <summary>
/// 将游戏原生“升级卡牌”入口桥接为蛊虫升一转。
///
/// 五转蛊不能借升级效果突破到六转；六转之后则仍可由升级效果
/// 逐次升到七转及更高转数。整个流程不调用原生 OnUpgrade，避免把
/// 蛊虫误变成普通“+”牌。
/// </summary>
internal static class GuWormUpgradePatch
{
    private const string HarmonyId =
        Entry.ModId + ".GuWormUpgrade";

    private static readonly FieldInfo? UpgradedEventField =
        AccessTools.Field(typeof(CardModel), nameof(CardModel.Upgraded));

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

        MethodInfo? upgradeInternal = AccessTools.DeclaredMethod(
            typeof(CardModel),
            nameof(CardModel.UpgradeInternal)
        );

        if (upgradeInternal == null)
        {
            throw new MissingMethodException(
                "蛊虫升转桥接所需的 CardModel.UpgradeInternal 不存在。"
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
        harmony.Patch(
            upgradeInternal,
            prefix: new HarmonyMethod(
                typeof(GuWormUpgradePatch),
                nameof(UpgradeInternalPrefix)
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
        switch (__instance)
        {
            case AbstractGuWormCard gu:
                __result = CanReceiveUpgradeEffect(
                    gu.BaseGuRank,
                    gu.MaxGuRank
                );
                break;
            case AbstractBenMingGuCard benMingGu:
                __result = CanReceiveUpgradeEffect(
                    benMingGu.GuRank,
                    benMingGu.MaxGuRank
                );
                break;
        }
    }

    private static void IsUpgradedPostfix(
        CardModel __instance,
        ref bool __result
    )
    {
        switch (__instance)
        {
            case AbstractGuWormCard gu:
                __result =
                    gu.BaseGuRank >=
                    GuZhenRenCardRules.XianGuRank;
                break;
            case AbstractBenMingGuCard benMingGu:
                __result =
                    benMingGu.GuRank >=
                    GuZhenRenCardRules.XianGuRank;
                break;
        }
    }

    private static bool UpgradeInternalPrefix(CardModel __instance)
    {
        bool isGuCard;
        bool increased;

        switch (__instance)
        {
            case AbstractGuWormCard gu:
                isGuCard = true;
                increased = CanReceiveUpgradeEffect(
                        gu.BaseGuRank,
                        gu.MaxGuRank
                    ) &&
                    gu.TryIncreaseGuRank();
                break;
            case AbstractBenMingGuCard benMingGu:
                isGuCard = true;
                increased = CanReceiveUpgradeEffect(
                        benMingGu.GuRank,
                        benMingGu.MaxGuRank
                    ) &&
                    benMingGu.TryIncreaseGuRank();
                break;
            default:
                isGuCard = false;
                increased = false;
                break;
        }

        if (!isGuCard)
        {
            return true;
        }

        if (increased)
        {
            // 保留原生升级监听器的刷新语义，但跳过 CurrentUpgradeLevel、
            // OnUpgrade 与原生数值升级。
            (UpgradedEventField?.GetValue(__instance) as Action)?.Invoke();
        }

        return false;
    }

    private static bool CanReceiveUpgradeEffect(
        int currentRank,
        int maximumRank
    ) =>
        currentRank != GuZhenRenCardRules.XianGuRank - 1 &&
        currentRank < maximumRank;
}
