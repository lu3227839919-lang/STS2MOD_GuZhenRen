using MegaCrit.Sts2.Core.Entities.Creatures;

namespace GuZhenRen.Multiplayer;

/// <summary>
/// 多人端之间必须使用相同的实体顺序后再消费同步随机数。
/// IEnumerable 的枚举顺序不是网络协议的一部分，直接随机索引可能让
/// 各端选中不同目标。
/// </summary>
internal static class GuZhenRenDeterminism
{
    internal static Creature[] OrderCreatures(
        IEnumerable<Creature> creatures
    )
    {
        ArgumentNullException.ThrowIfNull(creatures);

        return creatures
            .OrderBy(creature =>
                creature.CombatId.HasValue ? 0 : 1
            )
            .ThenBy(creature =>
                creature.CombatId ?? uint.MaxValue
            )
            .ToArray();
    }
}
