using GuZhenRen.Tribulations.Core;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRen.Tribulations.Contracts;

public interface ITribulationDefinition
{
    string Id { get; }
    TribulationTier Tier { get; }
    TribulationDanger Danger { get; }
    int BaseWeight { get; }
    bool CanAppear(in TribulationSelectionContext context);
    float GetEnemyCompatibilityMultiplier(in TribulationSelectionContext context);
}
