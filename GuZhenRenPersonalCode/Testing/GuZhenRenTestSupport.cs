using GuZhenRen.Aperture;
using GuZhenRen.Tribulations.Contracts;
using GuZhenRen.Tribulations.Core;
using GuZhenRen.Tribulations.Generation;

using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;

namespace GuZhenRen.Testing;

/// <summary>
/// 测试命令辅助逻辑。只通过空窍现有的保存事务和恢复入口推进状态，
/// 不直接绕过正式的升转副作用或灾劫应用流程。
/// </summary>
internal static class GuZhenRenTestSupport
{
    internal static async Task<int> GrantCultivationAsync(
        Player player,
        int amount
    )
    {
        ArgumentNullException.ThrowIfNull(player);

        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                amount,
                "修为数量必须大于零。"
            );
        }

        int applied = 0;

        while (applied < amount)
        {
            ApertureTransition transition = default;
            bool cultivationComplete = false;

            ApertureSystem.ModifyTribulationData(
                player,
                data =>
                {
                    data.Normalize();
                    if (data.IsCultivationComplete)
                    {
                        cultivationComplete = true;
                        return;
                    }

                    transition = ApertureProgression.GainVictoryXp(
                        data,
                        1
                    );

                    if (transition.RankChanged)
                    {
                        data.PendingRankAdvanceFrom =
                            transition.PreviousRank;
                        data.PendingRankAdvanceTo =
                            transition.CurrentRank;
                    }
                }
            );

            if (cultivationComplete)
            {
                break;
            }

            applied++;

            if (transition.RankChanged)
            {
                // 正式房间恢复入口会结算本命蛊、最大生命、天人感应
                // 和扩展通知。逐转等待可避免多次突破被合并。
                await ApertureSystem.HandleRoomEnteredAsync(player);
            }
        }

        ApertureSystem.RefreshRelicVisualState(player);
        return applied;
    }

    internal static async Task<int> AdvanceToRankAsync(
        Player player,
        int targetRank
    )
    {
        ArgumentNullException.ThrowIfNull(player);

        int normalizedTarget = Math.Clamp(
            targetRank,
            ApertureProgression.MinimumRank,
            ApertureProgression.MaximumImplementedRank
        );
        ApertureRunData state = ApertureSystem.GetState(player);

        if (normalizedTarget < state.Rank)
        {
            throw new InvalidOperationException(
                $"测试命令不能把空窍从 {state.Rank} 转降到 " +
                $"{normalizedTarget} 转。"
            );
        }

        while (state.Rank < normalizedTarget)
        {
            int required = ApertureProgression.GetRequiredXp(
                state.Rank
            );
            int missing = Math.Max(1, required - state.Xp);
            int applied = await GrantCultivationAsync(
                player,
                missing
            );

            if (applied <= 0)
            {
                break;
            }

            state = ApertureSystem.GetState(player);
        }

        return ApertureSystem.GetState(player).Rank;
    }

    internal static async Task<string> ForceTribulationAsync(
        Player player,
        string? requestedId
    )
    {
        ArgumentNullException.ThrowIfNull(player);

        if (player.Creature.CombatState is not { } combat)
        {
            throw new InvalidOperationException(
                "灾劫只能在战斗中触发。"
            );
        }

        ApertureRunData data = ApertureSystem.GetState(player);
        int floor = player.RunState.TotalFloor;

        if (data.ActiveTribulationFloor == floor &&
            !string.IsNullOrWhiteSpace(data.ActiveTribulationId))
        {
            await TribulationSystem.TryPrepareCombatAsync(player);
            return data.ActiveTribulationId;
        }

        int requiredXp = ApertureProgression.GetRequiredXp(data.Rank);
        TribulationSelectionContext context = new(
            player,
            combat,
            data,
            data.Rank,
            data.Xp,
            requiredXp,
            floor,
            ResolveStage(data.Xp, requiredXp)
        );

        ITribulationDefinition[] compatible =
            TribulationSystem.Registry.Definitions
                .Where(definition => definition.CanAppear(context))
                .OrderBy(definition => definition.Id, StringComparer.Ordinal)
                .ToArray();

        ITribulationDefinition? definition;
        if (string.IsNullOrWhiteSpace(requestedId) ||
            requestedId.Equals(
                "random",
                StringComparison.OrdinalIgnoreCase
            ))
        {
            uint stableRoll = unchecked(
                (uint)(floor * 397) ^
                (uint)(data.Rank * 31) ^
                (uint)data.Xp
            );
            definition = compatible.Length == 0
                ? null
                : compatible[
                    (int)(stableRoll % (uint)compatible.Length)
                ];
        }
        else
        {
            definition = compatible.FirstOrDefault(candidate =>
                candidate.Id.Equals(
                    requestedId,
                    StringComparison.OrdinalIgnoreCase
                )
            );
        }

        if (definition == null)
        {
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(requestedId)
                    ? "当前战斗没有可用的灾劫。"
                    : $"找不到可用于当前战斗的灾劫：{requestedId}"
            );
        }

        Creature? leader = new TribulationLeaderSelector()
            .SelectLeader(combat.Enemies);
        if (leader?.CombatId is not uint leaderId)
        {
            throw new InvalidOperationException(
                "当前战斗没有可作为灾劫首领的敌人。"
            );
        }

        TribulationBalanceConfig config =
            TribulationBalanceConfig.Default;
        float multiplier = new TribulationHealthScaler()
            .GetMaxHpMultiplier(
                definition.Danger,
                data.Rank,
                config
            );
        TribulationSelection selection = new(
            definition.Id,
            definition.Tier,
            definition.Danger,
            leaderId,
            multiplier,
            data.Rank,
            data.Xp,
            floor,
            0UL
        );
        int originalLeaderMaxHp =
            leader.MonsterMaxHpBeforeModification ?? leader.MaxHp;
        TribulationHistoryPolicy history = new();

        ApertureSystem.SaveTribulationSelection(
            player,
            selection,
            originalLeaderMaxHp,
            saved => history.RecordSelection(saved, selection)
        );

        Entry.Logger.Info(
            $"[灾劫测试] Floor={floor} Selected={definition.Id} " +
            $"Leader={leaderId} HpMultiplier={multiplier:0.00}"
        );

        // 正式准备入口检测到已保存的选择后会直接恢复并应用它，
        // 因而不会再次经过概率判定。
        await TribulationSystem.TryPrepareCombatAsync(player);
        return definition.Id;
    }

    private static TribulationProgressStage ResolveStage(
        int xp,
        int requiredXp
    )
    {
        if (requiredXp <= 0 || xp >= requiredXp)
        {
            return TribulationProgressStage.Complete;
        }

        float progress = xp / (float)requiredXp;
        if (progress <= 0.33f)
        {
            return TribulationProgressStage.Early;
        }

        return progress <= 0.66f
            ? TribulationProgressStage.Mid
            : TribulationProgressStage.Late;
    }
}
