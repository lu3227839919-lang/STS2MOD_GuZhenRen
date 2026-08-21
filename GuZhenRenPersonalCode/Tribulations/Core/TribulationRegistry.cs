// ============================================================================
// 中文维护说明
// 文件职责：实现灾劫系统的领域模型、注册表、平衡配置与生成流程。
// 主要类型：TribulationRegistry。
// 实现要点：灾劫能力按所实现的细粒度接口由事件路由选择性分派。
// 维护约定：灾劫选择先持久化再应用；新增钩子时同步更新事件路由和幂等标记。
// ============================================================================
using GuZhenRen.Tribulations.Contracts;

namespace GuZhenRen.Tribulations.Core;

/// <summary>
/// 灾劫定义注册表。注册阶段校验空标识、保留层级、非正权重和重复标识；
/// 运行阶段只负责查询，不保存玩家状态，也不参与随机选择。
/// </summary>
public sealed class TribulationRegistry
{
    private readonly Dictionary<string, ITribulationDefinition> _definitions =
        new(StringComparer.Ordinal);

    /// <summary>当前已注册定义的只读视图。</summary>
    public IReadOnlyCollection<ITribulationDefinition> Definitions => _definitions.Values;

    /// <summary>注册普通灾劫定义；标识必须全局唯一且基础权重大于零。</summary>
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

    /// <summary>按稳定标识查询；不存在时抛错，以暴露损坏存档或注册遗漏。</summary>
    public ITribulationDefinition GetRequired(string id) =>
        _definitions.TryGetValue(id, out ITribulationDefinition? value)
            ? value
            : throw new KeyNotFoundException($"Unknown tribulation ID: {id}");

    public IEnumerable<ITribulationDefinition> GetByTier(TribulationTier tier) =>
        _definitions.Values.Where(d => d.Tier == tier);

    public void Clear() => _definitions.Clear();
}
