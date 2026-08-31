using System.Reflection;

using GuZhenRen.Cards;

using HarmonyLib;

using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRen.Patches;

/// <summary>
/// 在升炼专用范围内接管原生升级判断和升级克隆；范围外继续交给
/// GuWormUpgradePatch。最高优先级前缀可避免同一预览克隆被升转两次。
/// </summary>
internal static class GuRankUpPreviewPatch
{
    private const string HarmonyId =
        Entry.ModId + ".GuRankUpPreview";

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
        MethodInfo? upgradeInternal = AccessTools.DeclaredMethod(
            typeof(CardModel),
            nameof(CardModel.UpgradeInternal)
        );

        if (isUpgradableGetter == null || upgradeInternal == null)
        {
            throw new MissingMethodException(
                "升炼前后预览所需的 CardModel 成员不存在。"
            );
        }

        Harmony harmony = new(HarmonyId);
        harmony.Patch(
            isUpgradableGetter,
            postfix: new HarmonyMethod(
                typeof(GuRankUpPreviewPatch),
                nameof(IsUpgradablePostfix)
            )
        );
        harmony.Patch(
            upgradeInternal,
            prefix: new HarmonyMethod(
                typeof(GuRankUpPreviewPatch),
                nameof(UpgradeInternalPrefix)
            )
        );
        GuRankUpPreviewSupport.PatchUpgradeDescription(harmony);

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
            GuRankUpPreviewSupport.Reset();
        }
    }

    internal static IDisposable Begin(
        int remainingSlots,
        IEnumerable<CardModel> excludedCards
    ) => GuRankUpPreviewSupport.Begin(
        remainingSlots,
        excludedCards
    );

    [HarmonyPriority(Priority.Last)]
    private static void IsUpgradablePostfix(
        CardModel __instance,
        ref bool __result
    )
    {
        if (GuRankUpPreviewSupport.TryGetIsUpgradable(
                __instance,
                out bool previewResult
            ))
        {
            __result = previewResult;
        }
    }

    [HarmonyPriority(Priority.First)]
    private static bool UpgradeInternalPrefix(
        CardModel __instance
    )
    {
        if (!GuRankUpPreviewSupport.IsActive ||
            __instance is not AbstractGuWormCard gu)
        {
            return true;
        }

        if (GuRankUpPreviewSupport.TryIncreaseForPreview(gu))
        {
            (UpgradedEventField?.GetValue(__instance) as Action)?.Invoke();
        }

        // 跳过原方法和其余会改变状态的低优先级前缀。
        return false;
    }
}
