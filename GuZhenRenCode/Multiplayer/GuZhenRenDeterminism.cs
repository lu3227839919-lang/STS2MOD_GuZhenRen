using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRen.Multiplayer;

/// <summary>
/// 多人端之间必须使用相同的实体顺序后再消费同步随机数。
/// IEnumerable 的枚举顺序不是网络协议的一部分，直接随机索引可能让
/// 各端选中不同目标。
/// </summary>
internal static class GuZhenRenDeterminism
{
    /// <summary>
    /// 战斗卡网络编号由原生多人层同步，可作为同 ID、同转数卡牌的
    /// 最终稳定排序键。未登记的预览/牌组模型排在已登记战斗卡之后。
    /// </summary>
    internal static uint GetCardNetworkId(CardModel card)
    {
        ArgumentNullException.ThrowIfNull(card);

        return NetCombatCardDb.Instance.TryGetCardId(
            card,
            out uint netId
        )
            ? netId
            : uint.MaxValue;
    }

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
