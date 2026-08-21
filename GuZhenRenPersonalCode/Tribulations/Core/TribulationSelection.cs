namespace GuZhenRen.Tribulations.Core;

public readonly record struct TribulationSelection(
    string TribulationId,
    TribulationTier Tier,
    TribulationDanger Danger,
    uint LeaderCombatId,
    float MaxHpMultiplier,
    int CurrentRank,
    int CurrentXp,
    int Floor,
    ulong SelectionSeedTag
);
