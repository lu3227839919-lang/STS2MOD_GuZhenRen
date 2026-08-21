// ============================================================================
// 中文维护说明
// 文件职责：协调休息点自定义选项的顺序执行、去重与会话隔离。
// 主要类型：GuRestSiteChoiceCoordinator、QueueState、RestSiteOptionReferenceComparer、PlayerRestSiteKey。
// 实现要点：共享状态受锁保护；异步入口还需保持同一玩家的操作顺序。
// 维护约定：保持公开签名、存档键和多人确定性；异步命令必须等待结算完成。
// ============================================================================
using System.Runtime.CompilerServices;

using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Runs;

namespace GuZhenRen.RestSite;

/// <summary>
/// 按玩家串行执行自定义休息点选择。
/// <para>
/// 0.110 同步器启动远端 <c>ChooseOption</c> 任务时不会等待上一条消息；
/// 重复包或紧邻到达的消息可能并发修改同一选项列表。本协调器为每名玩家维护
/// 独立队尾，并在同一选项对象执行期间去重。
/// </para>
/// </summary>
internal static class GuRestSiteChoiceCoordinator
{
    private static readonly object SyncRoot = new();
    private static readonly Dictionary<PlayerRestSiteKey, QueueState> States = [];
    private static long _generation;

    /// <summary>开始新休息点会话；代数递增会让旧会话中仍在等待的任务自动失效。</summary>
    internal static void BeginRestSiteSession()
    {
        lock (SyncRoot)
        {
            _generation++;
            States.Clear();
        }
    }

    /// <summary>
    /// 把一次选择接到该玩家的队尾。同一选项对象已在途时直接返回原任务，
    /// 从而让重复网络消息共享同一结果。
    /// </summary>
    internal static Task<bool> EnqueueChoice(
        Player player,
        RestSiteOption selectedOption,
        Func<Task<bool>> action
    )
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(selectedOption);
        ArgumentNullException.ThrowIfNull(action);

        Task predecessor;
        TaskCompletionSource<bool> completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        PlayerRestSiteKey key;
        QueueState state;

        lock (SyncRoot)
        {
            key = new PlayerRestSiteKey(
                player.RunState,
                player.NetId,
                _generation
            );

            if (!States.TryGetValue(key, out QueueState? existing))
            {
                state = new QueueState();
                States.Add(key, state);
            }
            else
            {
                state = existing;
            }

            if (state.Pending.TryGetValue(selectedOption, out Task<bool>? pending))
            {
                return pending;
            }

            predecessor = state.Tail;
            state.Pending.Add(selectedOption, completion.Task);
            state.Tail = completion.Task;
        }

        _ = ExecuteQueuedChoiceAsync(
            key,
            state,
            selectedOption,
            predecessor,
            action,
            completion
        );

        return completion.Task;
    }

    internal static void Reset()
    {
        BeginRestSiteSession();
    }

    private static async Task ExecuteQueuedChoiceAsync(
        PlayerRestSiteKey key,
        QueueState state,
        RestSiteOption selectedOption,
        Task predecessor,
        Func<Task<bool>> action,
        TaskCompletionSource<bool> completion
    )
    {
        try
        {
            try
            {
                await predecessor;
            }
            catch
            {
                // 前一选择失败不应永久污染队列；当前选择仍需获得一次执行机会。
            }

            // 会话切换后旧任务只返回 false，禁止作用到新休息点的选项列表。
            if (!IsCurrent(key, state))
            {
                completion.TrySetResult(false);
                return;
            }

            bool result = await action();
            completion.TrySetResult(result);
        }
        catch (OperationCanceledException exception)
        {
            completion.TrySetCanceled(exception.CancellationToken);
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }
        finally
        {
            lock (SyncRoot)
            {
                if (States.TryGetValue(key, out QueueState? current) &&
                    ReferenceEquals(current, state))
                {
                    state.Pending.Remove(selectedOption);

                    if (ReferenceEquals(state.Tail, completion.Task) &&
                        state.Pending.Count == 0)
                    {
                        States.Remove(key);
                    }
                }
            }
        }
    }

    private static bool IsCurrent(
        PlayerRestSiteKey key,
        QueueState state
    )
    {
        lock (SyncRoot)
        {
            return key.Generation == _generation &&
                States.TryGetValue(key, out QueueState? current) &&
                ReferenceEquals(current, state);
        }
    }

    private sealed class QueueState
    {
        internal Task Tail { get; set; } = Task.CompletedTask;

        internal Dictionary<RestSiteOption, Task<bool>> Pending { get; } =
            new(RestSiteOptionReferenceComparer.Instance);
    }

    private sealed class RestSiteOptionReferenceComparer
        : IEqualityComparer<RestSiteOption>
    {
        internal static RestSiteOptionReferenceComparer Instance { get; } = new();

        public bool Equals(RestSiteOption? x, RestSiteOption? y) =>
            ReferenceEquals(x, y);

        public int GetHashCode(RestSiteOption obj) =>
            RuntimeHelpers.GetHashCode(obj);
    }

    private readonly struct PlayerRestSiteKey
        : IEquatable<PlayerRestSiteKey>
    {
        internal PlayerRestSiteKey(
            IRunState runState,
            ulong playerNetId,
            long generation
        )
        {
            RunState = runState;
            PlayerNetId = playerNetId;
            Generation = generation;
        }

        private IRunState RunState { get; }
        private ulong PlayerNetId { get; }
        internal long Generation { get; }

        public bool Equals(PlayerRestSiteKey other) =>
            ReferenceEquals(RunState, other.RunState) &&
            PlayerNetId == other.PlayerNetId &&
            Generation == other.Generation;

        public override bool Equals(object? obj) =>
            obj is PlayerRestSiteKey other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(
                RuntimeHelpers.GetHashCode(RunState),
                PlayerNetId,
                Generation
            );
    }
}
