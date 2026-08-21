using GuZhenRen.Tribulations.Core;

namespace GuZhenRen.Aperture;

public static class ApertureTribulationExtensions
{
    public static TribulationSelection ToTribulationSelection(this ApertureRunData data) => new(
        data.ActiveTribulationId,
        (TribulationTier)data.ActiveTribulationTier,
        (TribulationDanger)data.ActiveTribulationDanger,
        data.ActiveLeaderCombatId,
        data.ActiveTribulationMaxHpMultiplier,
        data.Rank,
        data.Xp,
        data.ActiveTribulationFloor,
        data.ActiveTribulationSelectionSeedTag);
}
