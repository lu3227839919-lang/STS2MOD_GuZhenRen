using System.Reflection;

using HarmonyLib;

using GuZhenRen.RestSite;

using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;

namespace GuZhenRen.Patches;

/// <summary>
/// 让合练/升炼的两个次数槽遵循原版 RestSiteSynchronizer 的多人流程：
/// 同一玩家的选择按消息到达顺序串行执行；第一次成功后只保留同类
/// 第二次数槽，第二次成功后正常结束。
/// </summary>
internal static class GuRestSiteMultiUsePatch
{
    private const string HarmonyId =
        Entry.ModId + ".RestSiteMultiUse";

    private static readonly AsyncLocal<int>
        ChooseOptionBypassDepth = new();

    private static MethodInfo? _chooseOptionMethod;
    private static bool _initialized;

    internal static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        MethodInfo beginRestSiteMethod =
            AccessTools.Method(
                typeof(RestSiteSynchronizer),
                nameof(RestSiteSynchronizer.BeginRestSite)
            )
            ?? throw new MissingMethodException(
                typeof(RestSiteSynchronizer).FullName,
                nameof(RestSiteSynchronizer.BeginRestSite)
            );

        MethodInfo localOptionHoveredMethod =
            AccessTools.Method(
                typeof(RestSiteSynchronizer),
                nameof(RestSiteSynchronizer.LocalOptionHovered),
                [typeof(RestSiteOption)]
            )
            ?? throw new MissingMethodException(
                typeof(RestSiteSynchronizer).FullName,
                nameof(RestSiteSynchronizer.LocalOptionHovered)
            );

        MethodInfo getHoveredOptionIndexMethod =
            AccessTools.Method(
                typeof(RestSiteSynchronizer),
                nameof(RestSiteSynchronizer.GetHoveredOptionIndex),
                [typeof(ulong)]
            )
            ?? throw new MissingMethodException(
                typeof(RestSiteSynchronizer).FullName,
                nameof(RestSiteSynchronizer.GetHoveredOptionIndex)
            );

        MethodInfo shouldDisableMethod =
            AccessTools.Method(
                typeof(Hook),
                nameof(Hook.ShouldDisableRemainingRestSiteOptions),
                [
                    typeof(IRunState),
                    typeof(Player)
                ]
            )
            ?? throw new MissingMethodException(
                typeof(Hook).FullName,
                nameof(Hook.ShouldDisableRemainingRestSiteOptions)
            );

        MethodInfo generateOptionsMethod =
            AccessTools.Method(
                typeof(RestSiteOption),
                nameof(RestSiteOption.Generate),
                [
                    typeof(Player)
                ]
            )
            ?? throw new MissingMethodException(
                typeof(RestSiteOption).FullName,
                nameof(RestSiteOption.Generate)
            );

        _chooseOptionMethod =
            AccessTools.Method(
                typeof(RestSiteSynchronizer),
                "ChooseOption",
                [
                    typeof(Player),
                    typeof(int)
                ]
            )
            ?? throw new MissingMethodException(
                typeof(RestSiteSynchronizer).FullName,
                "ChooseOption"
            );

        Harmony harmony = new(HarmonyId);

        harmony.Patch(
            beginRestSiteMethod,
            prefix: new HarmonyMethod(
                typeof(GuRestSiteMultiUsePatch),
                nameof(BeginRestSitePrefix)
            )
        );

        harmony.Patch(
            localOptionHoveredMethod,
            prefix: new HarmonyMethod(
                typeof(GuRestSiteMultiUsePatch),
                nameof(LocalOptionHoveredPrefix)
            )
        );

        harmony.Patch(
            getHoveredOptionIndexMethod,
            postfix: new HarmonyMethod(
                typeof(GuRestSiteMultiUsePatch),
                nameof(GetHoveredOptionIndexPostfix)
            )
        );

        harmony.Patch(
            shouldDisableMethod,
            prefix: new HarmonyMethod(
                typeof(GuRestSiteMultiUsePatch),
                nameof(ShouldDisableRemainingOptionsPrefix)
            )
        );

        harmony.Patch(
            generateOptionsMethod,
            postfix: new HarmonyMethod(
                typeof(GuRestSiteMultiUsePatch),
                nameof(GenerateOptionsPostfix)
            )
        );

        harmony.Patch(
            _chooseOptionMethod,
            prefix: new HarmonyMethod(
                typeof(GuRestSiteMultiUsePatch),
                nameof(ChooseOptionPrefix)
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
            ChooseOptionBypassDepth.Value = 0;
            _chooseOptionMethod = null;
            GuRestSiteMultiUseCoordinator.Reset();
            _initialized = false;
        }
    }

    private static void BeginRestSitePrefix()
    {
        GuRestSiteMultiUseCoordinator.BeginRestSiteSession();
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

        RestSiteOption hoveredOption = option;

        bool stillPresent = __instance
            .GetLocalOptions()
            .Any(current =>
                ReferenceEquals(current, hoveredOption)
            );

        if (!stillPresent)
        {
            // 按钮列表在第一次双次操作后会被重建。旧按钮迟到的
            // hover/unhover 回调不能把 -1 转成 uint.MaxValue 发到网络。
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
        int optionCount = __instance
            .GetOptionsForPlayer(playerId)
            .Count;

        if (index < 0 || index >= optionCount)
        {
            // 迟到的悬停消息仍可能携带旧列表索引。原版 UI 会直接
            // 用该索引访问列表，因此在公开 getter 边界统一钳制为空。
            __result = null;
        }
    }

    private static bool ShouldDisableRemainingOptionsPrefix(
        Player player,
        ref bool __result
    )
    {
        if (!GuRestSiteMultiUseCoordinator
            .ShouldPreserveRemainingOptions(player))
        {
            return true;
        }

        __result = false;
        return false;
    }

    private static void GenerateOptionsPostfix(
        Player player,
        ref List<RestSiteOption> __result
    )
    {
        GuRestSiteMultiUseCoordinator
            .NormalizeGeneratedOptions(
                player,
                __result
            );
    }

    /// <summary>
    /// 原版远端消息处理会直接启动异步 ChooseOption，而不会等待上一条
    /// 选择完成。这里对带有双次数选项的玩家替换为严格串行的包装任务。
    /// 入队时保存选项对象身份，执行时重新定位同一个对象；这样第一次
    /// 选择完成、列表索引收缩后，重复的旧消息不会误选第二次数槽。
    /// </summary>
    private static bool ChooseOptionPrefix(
        RestSiteSynchronizer __instance,
        Player player,
        int optionIndex,
        ref Task<bool> __result
    )
    {
        if (ChooseOptionBypassDepth.Value > 0 ||
            !GuRestSiteMultiUseCoordinator
                .ShouldSerializeChoices(player))
        {
            return true;
        }

        IReadOnlyList<RestSiteOption> options =
            __instance.GetOptionsForPlayer(player);

        if (optionIndex < 0 ||
            optionIndex >= options.Count)
        {
            Entry.Logger.Info(
                $"忽略无效的篝火选项索引：玩家 {player.NetId}，" +
                $"索引 {optionIndex}，当前数量 {options.Count}。"
            );
            __result = Task.FromResult(false);
            return false;
        }

        RestSiteOption selectedOption = options[optionIndex];

        __result = GuRestSiteMultiUseCoordinator
            .EnqueueChoice(
                player,
                token => ExecuteQueuedChoiceAsync(
                    __instance,
                    player,
                    selectedOption,
                    token
                )
            );

        return false;
    }

    private static async Task<bool> ExecuteQueuedChoiceAsync(
        RestSiteSynchronizer synchronizer,
        Player player,
        RestSiteOption selectedOption,
        GuRestSiteMultiUseCoordinator.RestSiteExecutionToken token
    )
    {
        IReadOnlyList<RestSiteOption> options =
            synchronizer.GetOptionsForPlayer(player);
        int currentIndex = FindOptionByIdentity(
            options,
            selectedOption
        );

        if (currentIndex < 0)
        {
            Entry.Logger.Info(
                "忽略过期或重复的篝火选择消息：" +
                $"玩家 {player.NetId}，选项 {selectedOption.OptionId}。"
            );
            return false;
        }

        IGuMultiUseRestSiteOption? multiUseOption =
            selectedOption as IGuMultiUseRestSiteOption;
        GuRestSiteMultiUseCoordinator.PendingHistoryMarker?
            pendingMarker = null;
        bool invocationCompleted = false;
        bool success = false;
        bool continuationPrepared = false;

        try
        {
            if (multiUseOption is not null)
            {
                pendingMarker = GuRestSiteMultiUseCoordinator
                    .AddPendingHistoryMarker(
                        player,
                        multiUseOption
                    );
            }

            success = await InvokeOriginalChooseOptionAsync(
                synchronizer,
                player,
                currentIndex
            );
            invocationCompleted = true;

            if (!GuRestSiteMultiUseCoordinator
                .IsTokenCurrent(token))
            {
                return success;
            }

            if (success &&
                multiUseOption?.UseSlot ==
                    GuRestSiteMultiUseCoordinator.FirstUseSlot)
            {
                try
                {
                    continuationPrepared =
                        GuRestSiteMultiUseCoordinator
                            .PruneToSecondUse(
                                synchronizer,
                                player,
                                multiUseOption
                            );
                }
                catch (Exception exception)
                {
                    // 第一次效果已经提交，不能在整理失败后让其他篝火收益
                    // 继续可用；安全策略是关闭剩余选项。
                    GuRestSiteMultiUseCoordinator
                        .ClearRemainingOptions(
                            synchronizer,
                            player
                        );

                    Entry.Logger.Info(
                        "整理第二次休息点选项失败，" +
                        $"已安全关闭剩余选项：{exception}"
                    );
                }
            }

            return success;
        }
        finally
        {
            // 原版调用正常返回时，它已经明确告诉我们效果是否成功，
            // 可以移除待定标记。若调用抛异常则保留标记，重连时按已消费
            // 处理，避免效果提交后掉线造成重复领取。
            if (invocationCompleted)
            {
                GuRestSiteMultiUseCoordinator
                    .RemovePendingHistoryMarker(
                        player,
                        pendingMarker
                    );
            }

            if (multiUseOption is not null)
            {
                GuRestSiteMultiUseCoordinator.CompleteChoice(
                    token,
                    multiUseOption,
                    success,
                    continuationPrepared
                );
            }
        }
    }

    private static int FindOptionByIdentity(
        IReadOnlyList<RestSiteOption> options,
        RestSiteOption selectedOption
    )
    {
        for (int index = 0;
             index < options.Count;
             index++)
        {
            if (ReferenceEquals(
                    options[index],
                    selectedOption
                ))
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
                "休息点同步补丁尚未初始化。"
            );

        ChooseOptionBypassDepth.Value++;

        try
        {
            object? invocationResult = method.Invoke(
                synchronizer,
                [
                    player,
                    optionIndex
                ]
            );

            if (invocationResult is not Task<bool> task)
            {
                throw new InvalidOperationException(
                    "RestSiteSynchronizer.ChooseOption " +
                    "没有返回 Task<bool>。"
                );
            }

            return await task;
        }
        catch (TargetInvocationException exception)
            when (exception.InnerException is not null)
        {
            throw exception.InnerException;
        }
        finally
        {
            ChooseOptionBypassDepth.Value--;
        }
    }
}
