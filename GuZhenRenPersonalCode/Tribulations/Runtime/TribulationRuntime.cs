using GuZhenRen.Aperture;
using GuZhenRen.Tribulations.Contracts;
using GuZhenRen.Tribulations.Core;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;

namespace GuZhenRen.Tribulations.Runtime;

public sealed class TribulationRuntime(TribulationRegistry registry)
{
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

    public TribulationContext? TryBuildActiveContext(Player player)
    {
        ApertureRunData data = ApertureSystem.GetState(player);
        if (data.ActiveTribulationFloor != player.RunState.TotalFloor ||
            string.IsNullOrEmpty(data.ActiveTribulationId) ||
            player.Creature.CombatState == null)
            return null;

        TribulationSelection selection = data.ToTribulationSelection();
        Creature? leader = ResolveLeader(player, selection.LeaderCombatId);
        return leader == null ? null : BuildContext(player, leader, selection, data);
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
