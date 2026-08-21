using GuZhenRen.Aperture;
using GuZhenRen.Tribulations.EarthCalamities;
using GuZhenRen.Tribulations.Generation;
using GuZhenRen.Tribulations.Runtime;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Rooms;

namespace GuZhenRen.Tribulations.Core;

public static class TribulationSystem
{
    private static readonly TribulationBalanceConfig Config = TribulationBalanceConfig.Default;
    private static readonly TribulationRegistry RegistryInstance = new();
    private static readonly TribulationHistoryPolicy History = new();
    private static readonly TribulationRuntime RuntimeInstance = new(RegistryInstance);
    private static readonly TribulationGenerator Generator = new(
        RegistryInstance,
        new TribulationTriggerPolicy(Config),
        new TribulationWeightResolver(),
        History,
        new TribulationLeaderSelector(),
        new TribulationHealthScaler(),
        Config);

    public static TribulationRegistry Registry => RegistryInstance;
    public static TribulationEventRouter EventRouter { get; } = new(RegistryInstance, RuntimeInstance);

    public static void Initialize()
    {
        if (RegistryInstance.Definitions.Count == 0)
            EarthCalamityCatalog.RegisterAll(RegistryInstance);
        Entry.Logger.Info("[灾劫] 已注册 12 个地灾，统一事件路由已启用。");
    }

    public static void Uninitialize() => RegistryInstance.Clear();

    public static async Task TryPrepareCombatAsync(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!ApertureSystem.IsInitialized) return;

        ApertureRunData data = ApertureSystem.GetState(player);
        int floor = player.RunState.TotalFloor;

        if (data.ActiveTribulationFloor == floor && !string.IsNullOrEmpty(data.ActiveTribulationId))
        {
            await RuntimeInstance.ApplyAsync(data.ToTribulationSelection(), player);
            return;
        }

        if (RegistryInstance.Definitions.Count == 0) return;
        if (player.RunState.CurrentRoom?.RoomType == RoomType.Boss) return;
        if (player.Creature.CombatState is not { } combat) return;

        int requiredXp = ApertureProgression.GetRequiredXp(data.Rank);
        TribulationProgressStage stage = ResolveStage(data.Xp, requiredXp);
        if (stage == TribulationProgressStage.Complete) return;

        TribulationSelectionContext context = new(
            player, combat, data, data.Rank, data.Xp, requiredXp, floor, stage);
        TribulationSelection? selection = Generator.TryGenerate(context);
        if (selection == null) return;

        int originalLeaderMaxHp = combat.Enemies
            .First(c => c.CombatId == selection.Value.LeaderCombatId)
            .MaxHp;
        ApertureSystem.SaveTribulationSelection(
            player,
            selection.Value,
            originalLeaderMaxHp,
            d => History.RecordSelection(d, selection.Value));
        Entry.Logger.Info(
            $"[灾劫] Floor={floor} Rank={data.Rank} Xp={data.Xp}/{requiredXp} " +
            $"Selected={selection.Value.TribulationId} Tier={selection.Value.Tier} " +
            $"Leader={selection.Value.LeaderCombatId} HpMultiplier={selection.Value.MaxHpMultiplier:0.00}");
        await RuntimeInstance.ApplyAsync(selection.Value, player);
    }

    public static Task ResolveVictoryAsync(Player player) => RuntimeInstance.ResolveVictoryAsync(player);

    public static Task ResolveCombatEndAsync(Player player) =>
        RuntimeInstance.ResolveCombatEndAsync(player);

    private static TribulationProgressStage ResolveStage(int xp, int requiredXp)
    {
        if (requiredXp <= 0 || xp >= requiredXp) return TribulationProgressStage.Complete;
        float progress = xp / (float)requiredXp;
        if (progress <= 0.33f) return TribulationProgressStage.Early;
        if (progress <= 0.66f) return TribulationProgressStage.Mid;
        return TribulationProgressStage.Late;
    }
}
