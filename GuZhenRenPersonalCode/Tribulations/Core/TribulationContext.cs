using GuZhenRen.Aperture;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;

namespace GuZhenRen.Tribulations.Core;

public readonly record struct TribulationSelectionContext(
    Player Player,
    ICombatState Combat,
    ApertureRunData RunData,
    int Rank,
    int Xp,
    int RequiredXp,
    int Floor,
    TribulationProgressStage Stage
);

public sealed class TribulationContext
{
    public required Player Player { get; init; }
    public required ICombatState Combat { get; init; }
    public required Creature Leader { get; init; }
    public required TribulationSelection Selection { get; init; }
    public required ApertureRunData RunData { get; init; }

    public int CurrentRank => Selection.CurrentRank;
    public int Floor => Selection.Floor;
}
