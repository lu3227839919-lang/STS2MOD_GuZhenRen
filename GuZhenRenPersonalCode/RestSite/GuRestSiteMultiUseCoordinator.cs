using System.Threading;

using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;

namespace GuZhenRen.RestSite;

/// <summary>
/// 由合练、升炼共享的休息点事务协调器。
///
/// 合练与升炼现在都只成功执行一次；升炼的一次操作共有 2 个槽位
/// （凡蛊各占 1 槽、仙蛊各占 2 槽）。
/// 本协调器仍负责联机选择串行化、异常中断标记与历史恢复。
/// </summary>
internal static class GuRestSiteMultiUseCoordinator
{
    internal const int FirstUseSlot = 1;
    internal const int SecondUseSlot = 2;
    internal const int MaximumUses = 1;

    private const string PendingHistoryPrefix =
        Entry.ModId + ":REST_PENDING:";

    private static readonly object SyncRoot = new();

    private static readonly HashSet<RestSiteKey>
        KnownMultiUseRestSites = [];

    private static readonly HashSet<RestSiteKey>
        CompletedRestSites = [];

    private static readonly Dictionary<RestSiteKey, string>
        ActiveFamilies = [];

    private static readonly Dictionary<RestSiteKey, string>
        PendingContinuations = [];

    private static readonly Dictionary<RestSiteKey, Task>
        ChoiceQueueTails = [];

    /// <summary>
    /// 当前异步 ChooseOption 调用所绑定的会话令牌。
    /// OnSelect 内部会通过该令牌写入第一次成功状态，避免断线重建后
    /// 旧任务把状态写进新休息点会话。
    /// </summary>
    private static readonly AsyncLocal<RestSiteExecutionToken?>
        ActiveExecutionToken = new();

    private static long _sessionGeneration;

    /// <summary>
    /// 每次原版 BeginRestSite 前调用。所有可恢复状态随后都从本层的
    /// RestSiteChoices 历史重新构建，旧异步队列通过代次失效。
    /// </summary>
    internal static void BeginRestSiteSession()
    {
        lock (SyncRoot)
        {
            _sessionGeneration++;
            KnownMultiUseRestSites.Clear();
            CompletedRestSites.Clear();
            ActiveFamilies.Clear();
            PendingContinuations.Clear();
            ChoiceQueueTails.Clear();
        }

        ActiveExecutionToken.Value = null;
    }

    internal static void PrepareForRestSite(Player player)
    {
        RestSiteKey currentKey = GetKey(player);

        lock (SyncRoot)
        {
            RemoveStaleEntries(
                KnownMultiUseRestSites,
                currentKey
            );
            RemoveStaleEntries(
                CompletedRestSites,
                currentKey
            );
            RemoveStaleEntries(
                ActiveFamilies,
                currentKey
            );
            RemoveStaleEntries(
                PendingContinuations,
                currentKey
            );
            RemoveStaleEntries(
                ChoiceQueueTails,
                currentKey
            );

            KnownMultiUseRestSites.Add(currentKey);
            RestoreFlowFromRunHistory(player, currentKey);
        }
    }

    internal static bool IsRestSiteCompleted(Player player)
    {
        RestSiteKey key = GetEffectiveKey(player);

        lock (SyncRoot)
        {
            return CompletedRestSites.Contains(key);
        }
    }

    internal static bool TryGetContinuationFamily(
        Player player,
        out string? familyId
    )
    {
        RestSiteKey key = GetEffectiveKey(player);

        lock (SyncRoot)
        {
            return ActiveFamilies.TryGetValue(
                key,
                out familyId
            );
        }
    }

    /// <summary>
    /// 在 RestSiteOption.Generate 的所有模型钩子结束后再次规范列表，
    /// 防止其他模型在断线恢复的第二次数槽之后又追加额外选项。
    /// </summary>
    internal static void NormalizeGeneratedOptions(
        Player player,
        IList<RestSiteOption> options
    )
    {
        RestSiteKey key = GetKey(player);
        bool completed;
        string? activeFamily;

        lock (SyncRoot)
        {
            if (!KnownMultiUseRestSites.Contains(key))
            {
                return;
            }

            completed = CompletedRestSites.Contains(key);
            ActiveFamilies.TryGetValue(
                key,
                out activeFamily
            );
        }

        if (completed)
        {
            options.Clear();
            return;
        }

        if (activeFamily is null)
        {
            return;
        }

        RestSiteOption? continuation = options
            .FirstOrDefault(option =>
                option is IGuMultiUseRestSiteOption multiUse &&
                multiUse.UseSlot == SecondUseSlot &&
                string.Equals(
                    multiUse.FamilyId,
                    activeFamily,
                    StringComparison.Ordinal
                )
            );

        options.Clear();

        continuation ??= CreateContinuationOption(
            player,
            activeFamily
        );

        // 第一次操作后材料可能已经耗尽。只留下一个灰色按钮会让
        // 多人休息点永远无法结束，因此不可用的第二次数槽直接完成。
        if (continuation?.IsEnabled == true)
        {
            options.Add(continuation);
            return;
        }

        MarkCompleted(key);
    }

    internal static bool ShouldSerializeChoices(Player player)
    {
        RestSiteKey key = GetEffectiveKey(player);

        lock (SyncRoot)
        {
            return KnownMultiUseRestSites.Contains(key) ||
                ChoiceQueueTails.ContainsKey(key);
        }
    }

    /// <summary>
    /// 将同一玩家在当前休息点的选择严格串行化。
    /// </summary>
    internal static Task<bool> EnqueueChoice(
        Player player,
        Func<RestSiteExecutionToken, Task<bool>> action
    )
    {
        RestSiteKey key = GetKey(player);
        Task predecessor;
        long generation;
        TaskCompletionSource<bool> completion =
            new(
                TaskCreationOptions
                    .RunContinuationsAsynchronously
            );

        lock (SyncRoot)
        {
            generation = _sessionGeneration;
            predecessor = ChoiceQueueTails.TryGetValue(
                key,
                out Task? currentTail
            )
                ? currentTail
                : Task.CompletedTask;

            ChoiceQueueTails[key] = completion.Task;
        }

        RestSiteExecutionToken token = new(
            key,
            generation
        );

        return RunQueuedChoiceAsync();

        async Task<bool> RunQueuedChoiceAsync()
        {
            RestSiteExecutionToken? previousToken =
                ActiveExecutionToken.Value;

            try
            {
                await predecessor;

                if (!IsTokenCurrent(token))
                {
                    return false;
                }

                ActiveExecutionToken.Value = token;
                return await action(token);
            }
            finally
            {
                ActiveExecutionToken.Value = previousToken;
                completion.TrySetResult(true);

                lock (SyncRoot)
                {
                    if (ChoiceQueueTails.TryGetValue(
                            key,
                            out Task? currentTail
                        ) &&
                        ReferenceEquals(
                            currentTail,
                            completion.Task
                        ))
                    {
                        ChoiceQueueTails.Remove(key);
                    }
                }
            }
        }
    }

    internal static bool IsSlotEnabled(
        Player player,
        string familyId,
        int useSlot
    )
    {
        ValidateUseSlot(useSlot);
        RestSiteKey key = GetEffectiveKey(player);

        lock (SyncRoot)
        {
            if (CompletedRestSites.Contains(key))
            {
                return false;
            }

            // 两种自定义篝火操作都只有一个使用槽。
            // ActiveFamilies 只保留用于兼容旧会话；存在时同样不再开放
            // 第二次数槽。
            return useSlot == FirstUseSlot &&
                !ActiveFamilies.ContainsKey(key);
        }
    }

    internal static void MarkFirstUseSucceeded(
        Player player,
        string familyId
    )
    {
        RestSiteKey key = GetEffectiveKey(player);
        RestSiteExecutionToken? token =
            ActiveExecutionToken.Value;

        lock (SyncRoot)
        {
            if (token.HasValue &&
                !IsTokenCurrentUnsafe(token.Value))
            {
                return;
            }

            // 兼容旧版调用：第一次成功即完成当前休息点，
            // 不再登记任何第二次数槽。
            MarkCompletedUnsafe(key);
        }
    }

    /// <summary>
    /// Harmony 前缀在原版 ShouldDisableRemainingRestSiteOptions 前调用。
    /// 只有第一次自定义操作刚成功时才返回 true。
    /// </summary>
    internal static bool ShouldPreserveRemainingOptions(
        Player player
    )
    {
        RestSiteKey key = GetEffectiveKey(player);
        RestSiteExecutionToken? token =
            ActiveExecutionToken.Value;

        lock (SyncRoot)
        {
            if (token.HasValue &&
                !IsTokenCurrentUnsafe(token.Value))
            {
                return false;
            }

            return PendingContinuations.ContainsKey(key);
        }
    }

    /// <summary>
    /// 在执行效果前写入一个可保存的“待定次数”标记。若进程在效果提交后、
    /// 原版写入 OptionId 前中断，重连时会保守地把该次数视为已消费，防止
    /// 同一个篝火收益被重复领取。
    /// </summary>
    internal static PendingHistoryMarker? AddPendingHistoryMarker(
        Player player,
        IGuMultiUseRestSiteOption option
    )
    {
        string marker = BuildPendingHistoryMarker(option);
        List<string>? choices = player.RunState
            .CurrentMapPointHistoryEntry?
            .GetEntry(player.NetId)
            .RestSiteChoices;

        if (choices == null)
        {
            return null;
        }

        if (!choices.Contains(marker))
        {
            choices.Add(marker);
        }

        // 保存具体列表引用，避免异步选择结束时玩家已移动到下一地图点，
        // 从而把旧事务标记错误地从新地图点历史中移除。
        return new PendingHistoryMarker(
            player.RunState,
            player.NetId,
            choices,
            marker
        );
    }

    internal static void RemovePendingHistoryMarker(
        Player player,
        PendingHistoryMarker? pendingMarker
    )
    {
        if (!pendingMarker.HasValue)
        {
            return;
        }

        PendingHistoryMarker marker = pendingMarker.Value;

        // 只允许同一跑局、同一玩家的调用移除标记；列表引用本身已经
        // 精确绑定到开始选择时的地图历史条目。
        if (!ReferenceEquals(
                marker.RunState,
                player.RunState
            ) ||
            marker.PlayerNetId != player.NetId)
        {
            return;
        }

        marker.Choices.Remove(marker.Marker);
    }

    /// <summary>
    /// 原版已经移除第一次数槽后，只保留同类且仍可用的第二次数槽。
    /// </summary>
    internal static bool PruneToSecondUse(
        RestSiteSynchronizer synchronizer,
        Player player,
        IGuMultiUseRestSiteOption selectedOption
    )
    {
        IReadOnlyList<RestSiteOption> options =
            synchronizer.GetOptionsForPlayer(player);

        if (options is not IList<RestSiteOption> mutableOptions)
        {
            throw new InvalidOperationException(
                "RestSiteSynchronizer 返回的选项集合不可修改。"
            );
        }

        RestSiteOption? continuation = mutableOptions
            .FirstOrDefault(candidate =>
                candidate is IGuMultiUseRestSiteOption multiUse &&
                multiUse.UseSlot == SecondUseSlot &&
                string.Equals(
                    multiUse.FamilyId,
                    selectedOption.FamilyId,
                    StringComparison.Ordinal
                )
            );

        continuation ??= CreateContinuationOption(
            player,
            selectedOption.FamilyId
        );

        mutableOptions.Clear();

        // 初始页面只显示第一次数槽；第一次成功后在这里按需创建并
        // 保留唯一的第二次数槽。
        if (continuation?.IsEnabled == true)
        {
            mutableOptions.Add(continuation);
            return true;
        }

        return false;
    }

    internal static void ClearRemainingOptions(
        RestSiteSynchronizer synchronizer,
        Player player
    )
    {
        IReadOnlyList<RestSiteOption> options =
            synchronizer.GetOptionsForPlayer(player);

        if (options is IList<RestSiteOption> mutableOptions)
        {
            mutableOptions.Clear();
        }
    }

    /// <summary>
    /// 必须在原版选择流程及第一次数槽整理完成后调用。
    /// </summary>
    internal static void CompleteChoice(
        RestSiteExecutionToken token,
        IGuMultiUseRestSiteOption option,
        bool success,
        bool continuationPrepared
    )
    {
        lock (SyncRoot)
        {
            if (!IsTokenCurrentUnsafe(token))
            {
                return;
            }

            RestSiteKey key = token.Key;
            PendingContinuations.Remove(key);

            if (option.UseSlot == FirstUseSlot)
            {
                if (!success)
                {
                    ActiveFamilies.Remove(key);
                }
                else if (!continuationPrepared)
                {
                    MarkCompletedUnsafe(key);
                }

                return;
            }

            if (success)
            {
                MarkCompletedUnsafe(key);
            }
        }
    }

    internal static bool IsTokenCurrent(
        RestSiteExecutionToken token
    )
    {
        lock (SyncRoot)
        {
            return IsTokenCurrentUnsafe(token);
        }
    }

    internal static void Reset()
    {
        lock (SyncRoot)
        {
            _sessionGeneration++;
            KnownMultiUseRestSites.Clear();
            CompletedRestSites.Clear();
            ActiveFamilies.Clear();
            PendingContinuations.Clear();
            ChoiceQueueTails.Clear();
        }

        ActiveExecutionToken.Value = null;
    }

    private static void RestoreFlowFromRunHistory(
        Player player,
        RestSiteKey key
    )
    {
        List<string>? choices = player.RunState
            .CurrentMapPointHistoryEntry?
            .GetEntry(player.NetId)
            .RestSiteChoices;

        if (choices is null || choices.Count == 0)
        {
            ActiveFamilies.Remove(key);
            CompletedRestSites.Remove(key);
            return;
        }

        // 待定标记表示上次中断点无法判断效果是否已经提交。
        // 保守地补成正式次数：可能少获得一次收益，但不会重复领取。
        NormalizePendingHistoryMarkers(choices);

        bool hasOrdinaryChoice = choices.Any(choice =>
            !IsCustomFamily(choice)
        );

        int rankUpUses = choices.Count(choice =>
            string.Equals(
                choice,
                GuRankUpRestSiteOption.OptionIdentifier,
                StringComparison.Ordinal
            )
        );
        int heLianUses = choices.Count(choice =>
            string.Equals(
                choice,
                GuHeLianRestSiteOption.OptionIdentifier,
                StringComparison.Ordinal
            )
        );

        // 原版休息/锻造/协助等任意普通选择都已经消耗了篝火。
        // 两种自定义流程混用同样属于异常完成态，不能继续发放收益。
        if (hasOrdinaryChoice ||
            rankUpUses >= MaximumUses ||
            heLianUses >= MaximumUses)
        {
            MarkCompletedUnsafe(key);
            return;
        }

        CompletedRestSites.Remove(key);
        ActiveFamilies.Remove(key);
    }

    private static void NormalizePendingHistoryMarkers(
        List<string> choices
    )
    {
        List<(string Marker, string FamilyId, int UseSlot)> pending = [];

        foreach (string choice in choices)
        {
            if (TryParsePendingHistoryMarker(
                    choice,
                    out string? familyId,
                    out int useSlot
                ))
            {
                pending.Add((
                    choice,
                    familyId!,
                    useSlot
                ));
            }
        }

        foreach ((string marker, string familyId, int useSlot) in pending)
        {
            choices.Remove(marker);

            int committedCount = choices.Count(choice =>
                string.Equals(
                    choice,
                    familyId,
                    StringComparison.Ordinal
                )
            );

            while (committedCount < useSlot)
            {
                choices.Add(familyId);
                committedCount++;
            }
        }
    }

    private static string BuildPendingHistoryMarker(
        IGuMultiUseRestSiteOption option
    )
    {
        return PendingHistoryPrefix +
            option.FamilyId +
            ":" +
            option.UseSlot;
    }

    private static bool TryParsePendingHistoryMarker(
        string choice,
        out string? familyId,
        out int useSlot
    )
    {
        familyId = null;
        useSlot = 0;

        if (!choice.StartsWith(
                PendingHistoryPrefix,
                StringComparison.Ordinal
            ))
        {
            return false;
        }

        string payload = choice[PendingHistoryPrefix.Length..];
        int separatorIndex = payload.LastIndexOf(':');

        if (separatorIndex <= 0 ||
            !int.TryParse(
                payload[(separatorIndex + 1)..],
                out useSlot
            ) ||
            useSlot is < FirstUseSlot or > SecondUseSlot)
        {
            return false;
        }

        string parsedFamily = payload[..separatorIndex];
        if (!IsCustomFamily(parsedFamily))
        {
            return false;
        }

        familyId = parsedFamily;
        return true;
    }

    private static bool IsCustomFamily(string choice)
    {
        return string.Equals(
                choice,
                GuRankUpRestSiteOption.OptionIdentifier,
                StringComparison.Ordinal
            ) ||
            string.Equals(
                choice,
                GuHeLianRestSiteOption.OptionIdentifier,
                StringComparison.Ordinal
            );
    }

    private static RestSiteOption? CreateContinuationOption(
        Player player,
        string activeFamily
    )
    {
        // 合练和升炼均为单次篝火操作，不再生成第二次数槽。
        _ = player;
        _ = activeFamily;
        return null;
    }

    private static RestSiteKey GetEffectiveKey(Player player)
    {
        RestSiteExecutionToken? token =
            ActiveExecutionToken.Value;

        if (token.HasValue &&
            token.Value.Key.PlayerNetId == player.NetId &&
            ReferenceEquals(
                token.Value.Key.RunState,
                player.RunState
            ))
        {
            return token.Value.Key;
        }

        return GetKey(player);
    }

    private static RestSiteKey GetKey(Player player)
    {
        return new RestSiteKey(
            player.RunState,
            player.NetId,
            player.RunState.RunLocation
        );
    }

    private static bool IsTokenCurrentUnsafe(
        RestSiteExecutionToken token
    )
    {
        return token.Generation == _sessionGeneration &&
            token.Key.RunState.RunLocation == token.Key.Location &&
            KnownMultiUseRestSites.Contains(token.Key);
    }

    private static void MarkCompleted(RestSiteKey key)
    {
        lock (SyncRoot)
        {
            MarkCompletedUnsafe(key);
        }
    }

    private static void MarkCompletedUnsafe(RestSiteKey key)
    {
        ActiveFamilies.Remove(key);
        PendingContinuations.Remove(key);
        CompletedRestSites.Add(key);
    }

    private static void ValidateUseSlot(int useSlot)
    {
        if (useSlot is < FirstUseSlot or > SecondUseSlot)
        {
            throw new ArgumentOutOfRangeException(
                nameof(useSlot),
                useSlot,
                $"休息点使用槽必须在 {FirstUseSlot} 到 " +
                $"{SecondUseSlot} 之间。"
            );
        }
    }

    private static void RemoveStaleEntries(
        HashSet<RestSiteKey> entries,
        RestSiteKey currentKey
    )
    {
        entries.RemoveWhere(key =>
            ReferenceEquals(key.RunState, currentKey.RunState) &&
            key.PlayerNetId == currentKey.PlayerNetId &&
            key != currentKey
        );
    }

    private static void RemoveStaleEntries<TValue>(
        Dictionary<RestSiteKey, TValue> entries,
        RestSiteKey currentKey
    )
    {
        RestSiteKey[] staleKeys = entries.Keys
            .Where(key =>
                ReferenceEquals(key.RunState, currentKey.RunState) &&
                key.PlayerNetId == currentKey.PlayerNetId &&
                key != currentKey
            )
            .ToArray();

        foreach (RestSiteKey staleKey in staleKeys)
        {
            entries.Remove(staleKey);
        }
    }

    internal readonly record struct PendingHistoryMarker(
        IRunState RunState,
        ulong PlayerNetId,
        List<string> Choices,
        string Marker
    );

    internal readonly record struct RestSiteExecutionToken(
        RestSiteKey Key,
        long Generation
    );

    internal readonly record struct RestSiteKey(
        IRunState RunState,
        ulong PlayerNetId,
        RunLocation Location
    );
}

/// <summary>
/// 供同步补丁识别合练/升炼次数槽，不依赖具体选项类型。
/// </summary>
public interface IGuMultiUseRestSiteOption
{
    string FamilyId { get; }

    int UseSlot { get; }
}
