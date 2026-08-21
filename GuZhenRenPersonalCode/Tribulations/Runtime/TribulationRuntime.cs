// ============================================================================
// 中文维护说明
// 文件职责：负责把已保存的灾劫选择恢复为战斗效果，并路由战斗事件。
// 主要类型：TribulationRuntime。
// 实现要点：灾劫能力按所实现的细粒度接口由事件路由选择性分派。
// 实现补充：胜利结算带持久化幂等标记，重连或回调重放不得重复发奖。
// 实现补充：战斗变更通过命令队列并等待完成，不直接绕过游戏同步层修改结果。
// 维护约定：灾劫选择先持久化再应用；新增钩子时同步更新事件路由和幂等标记。
// ============================================================================
using GuZhenRen.Aperture;
using GuZhenRen.Tribulations.Contracts;
using GuZhenRen.Tribulations.Core;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;

namespace GuZhenRen.Tribulations.Runtime;

/// <summary>
/// 把已持久化的灾劫选择应用到战斗，并负责胜利和战斗结束的幂等结算。
/// 此类不重新随机，只解释已经保存的选择。
/// </summary>
public sealed class TribulationRuntime(TribulationRegistry registry)
{
    /// <summary>应用首领生命倍率和初始效果；选择与当前存档不一致时立即失败。</summary>
    public async Task ApplyAsync(TribulationSelection selection, Player player)
    {
        ApertureRunData data = ApertureSystem.GetState(player);
        if (data.ActiveTribulationFloor != selection.Floor ||
            !string.Equals(data.ActiveTribulationId, selection.TribulationId, StringComparison.Ordinal))
            throw new InvalidOperationException("Saved tribulation selection does not match runtime apply request.");

        if (data.ActiveTribulationApplied)
            return;

        Creature leader = ResolveLeader(player, selection.LeaderCombatId)
            ?? throw new InvalidOperationException(
                $"Tribulation leader {selection.LeaderCombatId} was not found.");

        // 始终以首次保存的原始生命为基准，避免重连后再次乘算。
        int originalMaxHp = data.OriginalLeaderMaxHp > 0
            ? data.OriginalLeaderMaxHp
            : leader.MonsterMaxHpBeforeModification ?? leader.MaxHp;
        int targetMaxHp = Math.Max(1, (int)Math.Ceiling(originalMaxHp * selection.MaxHpMultiplier));
        int hpIncrease = Math.Max(0, targetMaxHp - leader.MaxHp);

        if (hpIncrease > 0)
        {
            int oldCurrentHp = leader.CurrentHp;
            await CreatureCmd.SetMaxHp(leader, targetMaxHp);
            leader.SetCurrentHpInternal(Math.Min(targetMaxHp, oldCurrentHp + hpIncrease));
        }

        ITribulationDefinition definition = registry.GetRequired(selection.TribulationId);
        TribulationContext context = BuildContext(player, leader, selection, data);
        if (definition is ITribulationCombatLifecycle lifecycle)
            await lifecycle.OnAppliedAsync(context);

        ApertureSystem.MarkTribulationApplied(player, selection.Floor, originalMaxHp);
    }

    /// <summary>结算胜利钩子；持久标记保证重复回调不会重复发奖。</summary>
    public async Task ResolveVictoryAsync(Player player)
    {
        TribulationContext? context = TryBuildActiveContext(player);
        if (context == null) return;
        ApertureRunData data = context.RunData;
        if (data.ActiveTribulationVictoryResolved) return;

        ITribulationDefinition definition = registry.GetRequired(context.Selection.TribulationId);
        if (definition is ITribulationCombatLifecycle lifecycle)
            await lifecycle.OnCombatVictoryAsync(context);

        ApertureSystem.MarkTribulationVictoryResolved(player, context.Selection.Floor);
    }

    public async Task ResolveCombatEndAsync(Player player)
    {
        TribulationContext? context = TryBuildActiveContext(
            player,
            requireLivingLeader: false);
        if (context == null || context.RunData.ActiveTribulationEndResolved)
            return;

        ITribulationDefinition definition = registry.GetRequired(
            context.Selection.TribulationId);
        if (definition is ITribulationCombatLifecycle lifecycle)
            await lifecycle.OnCombatEndAsync(context);

        ApertureSystem.MarkTribulationEndResolved(
            player,
            context.Selection.Floor);
    }

    public TribulationContext? TryBuildActiveContext(
        Player player,
        bool requireLivingLeader = true)
    {
        ApertureRunData data = ApertureSystem.GetState(player);
        if (data.ActiveTribulationFloor != player.RunState.TotalFloor ||
            string.IsNullOrEmpty(data.ActiveTribulationId) ||
            player.Creature.CombatState == null)
            return null;

        TribulationSelection selection = data.ToTribulationSelection();
        Creature? leader = ResolveLeader(player, selection.LeaderCombatId);
        return leader == null || (requireLivingLeader && !leader.IsAlive)
            ? null
            : BuildContext(player, leader, selection, data);
    }

    private static Creature? ResolveLeader(Player player, uint combatId) =>
        player.Creature.CombatState?.Enemies.FirstOrDefault(c => c.CombatId == combatId);

    private static TribulationContext BuildContext(
        Player player,
        Creature leader,
        TribulationSelection selection,
        ApertureRunData data) => new()
    {
        Player = player,
        Combat = player.Creature.CombatState!,
        Leader = leader,
        Selection = selection,
        RunData = data,
    };
}
