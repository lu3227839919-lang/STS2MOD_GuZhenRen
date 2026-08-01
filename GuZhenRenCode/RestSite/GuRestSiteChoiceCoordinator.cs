using System.Runtime.CompilerServices;

using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Runs;

namespace GuZhenRen.RestSite;

/// <summary>
/// Serializes custom rest-site choices per player.
///
/// The 0.110 synchronizer starts remote ChooseOption tasks without awaiting the
/// previous message. A duplicated or rapidly-following packet can therefore
/// execute against a list that is being changed by another selection. This
/// coordinator keeps each player's custom choices ordered and deduplicates the
/// same option object while it is in flight.
/// </summary>
internal static class GuRestSiteChoiceCoordinator
{
    private static readonly object SyncRoot = new();
    private static readonly Dictionary<PlayerRestSiteKey, QueueState> States = [];
    private static long _generation;

    internal static void BeginRestSiteSession()
    {
        lock (SyncRoot)
        {
            _generation++;
            States.Clear();
        }
    }

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
                // A previous failed choice must not permanently poison the queue.
            }

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
