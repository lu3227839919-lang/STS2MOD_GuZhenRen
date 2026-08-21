using GuZhenRen.Tribulations.Contracts;

namespace GuZhenRen.Tribulations.Core;

public sealed class TribulationRegistry
{
    private readonly Dictionary<string, ITribulationDefinition> _definitions =
        new(StringComparer.Ordinal);

    public IReadOnlyCollection<ITribulationDefinition> Definitions => _definitions.Values;

    public void Register(ITribulationDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (string.IsNullOrWhiteSpace(definition.Id))
            throw new ArgumentException("Tribulation ID cannot be empty.", nameof(definition));
        if (definition.Tier is TribulationTier.None or TribulationTier.HeavenlyDaoBlockade)
            throw new ArgumentException($"Invalid ordinary tribulation tier: {definition.Tier}");
        if (definition.BaseWeight <= 0)
            throw new ArgumentException($"Tribulation {definition.Id} must have BaseWeight > 0.");
        if (!_definitions.TryAdd(definition.Id, definition))
            throw new InvalidOperationException($"Duplicate tribulation ID: {definition.Id}");
    }

    public ITribulationDefinition GetRequired(string id) =>
        _definitions.TryGetValue(id, out ITribulationDefinition? value)
            ? value
            : throw new KeyNotFoundException($"Unknown tribulation ID: {id}");

    public IEnumerable<ITribulationDefinition> GetByTier(TribulationTier tier) =>
        _definitions.Values.Where(d => d.Tier == tier);
}
