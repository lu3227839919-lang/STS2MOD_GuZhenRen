using System.Reflection;
using System.Runtime.ExceptionServices;

using GuZhenRen.RestSite;

using HarmonyLib;

using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Multiplayer.Game;

namespace GuZhenRen.Patches;

/// <summary>
/// Multiplayer safety for the two custom rest-site options.
///
/// Choices are serialized per player, duplicate in-flight packets reuse the
/// same task, and an option is located again by object identity immediately
/// before execution. This prevents a stale index from selecting a different
/// option after the list changes.
/// </summary>
internal static class GuRestSiteChoicePatch
{
    private const string HarmonyId = Entry.ModId + ".RestSiteChoice";

    private static readonly AsyncLocal<int> ChooseOptionBypassDepth = new();
    private static MethodInfo? _chooseOptionMethod;
    private static bool _initialized;

    internal static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        MethodInfo beginRestSite = AccessTools.DeclaredMethod(
            typeof(RestSiteSynchronizer),
            nameof(RestSiteSynchronizer.BeginRestSite),
            Type.EmptyTypes
        ) ?? throw Missing(nameof(RestSiteSynchronizer.BeginRestSite));

        MethodInfo localOptionHovered = AccessTools.DeclaredMethod(
            typeof(RestSiteSynchronizer),
            nameof(RestSiteSynchronizer.LocalOptionHovered),
            [typeof(RestSiteOption)]
        ) ?? throw Missing(nameof(RestSiteSynchronizer.LocalOptionHovered));

        MethodInfo getHoveredOptionIndex = AccessTools.DeclaredMethod(
            typeof(RestSiteSynchronizer),
            nameof(RestSiteSynchronizer.GetHoveredOptionIndex),
            [typeof(ulong)]
        ) ?? throw Missing(nameof(RestSiteSynchronizer.GetHoveredOptionIndex));

        _chooseOptionMethod = AccessTools.DeclaredMethod(
            typeof(RestSiteSynchronizer),
            "ChooseOption",
            [typeof(Player), typeof(int)]
        ) ?? throw Missing("ChooseOption");

        Harmony harmony = new(HarmonyId);

        try
        {
            harmony.Patch(
                beginRestSite,
                prefix: new HarmonyMethod(
                    typeof(GuRestSiteChoicePatch),
                    nameof(BeginRestSitePrefix)
                )
            );
            harmony.Patch(
                localOptionHovered,
                prefix: new HarmonyMethod(
                    typeof(GuRestSiteChoicePatch),
                    nameof(LocalOptionHoveredPrefix)
                )
            );
            harmony.Patch(
                getHoveredOptionIndex,
                postfix: new HarmonyMethod(
                    typeof(GuRestSiteChoicePatch),
                    nameof(GetHoveredOptionIndexPostfix)
                )
            );
            harmony.Patch(
                _chooseOptionMethod,
                prefix: new HarmonyMethod(
                    typeof(GuRestSiteChoicePatch),
                    nameof(ChooseOptionPrefix)
                )
            );

            _initialized = true;
        }
        catch
        {
            harmony.UnpatchAll(HarmonyId);
            _chooseOptionMethod = null;
            throw;
        }
    }

    internal static void Uninitialize()
    {
        try
        {
            new Harmony(HarmonyId).UnpatchAll(HarmonyId);
        }
        finally
        {
            ChooseOptionBypassDepth.Value = 0;
            _chooseOptionMethod = null;
            GuRestSiteChoiceCoordinator.Reset();
            _initialized = false;
        }
    }

    private static MissingMethodException Missing(string methodName) =>
        new(typeof(RestSiteSynchronizer).FullName, methodName);

    private static void BeginRestSitePrefix()
    {
        GuRestSiteChoiceCoordinator.BeginRestSiteSession();
    }

    private static void LocalOptionHoveredPrefix(
        RestSiteSynchronizer __instance,
        ref RestSiteOption? option
    )
    {
        if (option is null)
        {
            return;
        }

        RestSiteOption hovered = option;
        if (!__instance.GetLocalOptions().Any(
                current => ReferenceEquals(current, hovered)
            ))
        {
            option = null;
        }
    }

    private static void GetHoveredOptionIndexPostfix(
        RestSiteSynchronizer __instance,
        ulong playerId,
        ref int? __result
    )
    {
        if (!__result.HasValue)
        {
            return;
        }

        int index = __result.Value;
        int count = __instance.GetOptionsForPlayer(playerId).Count;
        if (index < 0 || index >= count)
        {
            __result = null;
        }
    }

    private static bool ChooseOptionPrefix(
        RestSiteSynchronizer __instance,
        Player player,
        int optionIndex,
        ref Task<bool> __result
    )
    {
        if (ChooseOptionBypassDepth.Value > 0)
        {
            return true;
        }

        IReadOnlyList<RestSiteOption> options =
            __instance.GetOptionsForPlayer(player);

        if (optionIndex < 0 || optionIndex >= options.Count)
        {
            Entry.Logger.Info(
                $"忽略无效的篝火选项索引：玩家 {player.NetId}，" +
                $"索引 {optionIndex}，当前数量 {options.Count}。"
            );
            __result = Task.FromResult(false);
            return false;
        }

        RestSiteOption selectedOption = options[optionIndex];
        if (selectedOption is not GuRankUpRestSiteOption &&
            selectedOption is not GuHeLianRestSiteOption)
        {
            return true;
        }

        __result = GuRestSiteChoiceCoordinator.EnqueueChoice(
            player,
            selectedOption,
            () => ExecuteQueuedChoiceAsync(
                __instance,
                player,
                selectedOption
            )
        );
        return false;
    }

    private static async Task<bool> ExecuteQueuedChoiceAsync(
        RestSiteSynchronizer synchronizer,
        Player player,
        RestSiteOption selectedOption
    )
    {
        IReadOnlyList<RestSiteOption> options =
            synchronizer.GetOptionsForPlayer(player);
        int currentIndex = FindByIdentity(options, selectedOption);

        if (currentIndex < 0)
        {
            Entry.Logger.Info(
                "忽略过期或重复的篝火选择消息：" +
                $"玩家 {player.NetId}，选项 {selectedOption.OptionId}。"
            );
            return false;
        }

        return await InvokeOriginalChooseOptionAsync(
            synchronizer,
            player,
            currentIndex
        );
    }

    private static int FindByIdentity(
        IReadOnlyList<RestSiteOption> options,
        RestSiteOption selectedOption
    )
    {
        for (int index = 0; index < options.Count; index++)
        {
            if (ReferenceEquals(options[index], selectedOption))
            {
                return index;
            }
        }

        return -1;
    }

    private static async Task<bool> InvokeOriginalChooseOptionAsync(
        RestSiteSynchronizer synchronizer,
        Player player,
        int optionIndex
    )
    {
        MethodInfo method = _chooseOptionMethod
            ?? throw new InvalidOperationException(
                "RestSiteSynchronizer.ChooseOption 尚未完成初始化。"
            );

        ChooseOptionBypassDepth.Value++;
        try
        {
            object? invocation;
            try
            {
                invocation = method.Invoke(
                    synchronizer,
                    [player, optionIndex]
                );
            }
            catch (TargetInvocationException exception)
                when (exception.InnerException is not null)
            {
                ExceptionDispatchInfo.Capture(
                    exception.InnerException
                ).Throw();
                throw;
            }

            if (invocation is not Task<bool> task)
            {
                throw new InvalidOperationException(
                    "RestSiteSynchronizer.ChooseOption 返回了意外类型。"
                );
            }

            return await task;
        }
        finally
        {
            ChooseOptionBypassDepth.Value = Math.Max(
                0,
                ChooseOptionBypassDepth.Value - 1
            );
        }
    }
}
