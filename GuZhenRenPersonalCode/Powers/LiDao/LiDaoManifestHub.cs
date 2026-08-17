using GuZhenRen.Cards.LiDao;

using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace GuZhenRen.Powers.LiDao;

/// <summary>
/// 兽力虚影显化事件分发中枢。
///
/// - 自然显化（攻击触发成功后）：先交群力判定（可能产生额外显化），
///   再交我力按显化类型计数。
/// - 6转起的群力额外显化：只交我力按“实际显化”计数，绝不回调群力，
///   避免递归；5转的群力额外显化不进入该事件。
/// </summary>
public static class LiDaoManifestHub
{
    public static async Task NotifyNaturalManifestAsync(
        PlayerChoiceContext choiceContext,
        Player owner,
        AbstractLiDaoXuYing phantom,
        CardPlay cardPlay
    )
    {
        ArgumentNullException.ThrowIfNull(choiceContext);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(phantom);
        ArgumentNullException.ThrowIfNull(cardPlay);

        Creature creature = owner.Creature;

        QunLiPower? group = creature.GetPower<QunLiPower>();
        if (group != null)
        {
            await group.HandleNaturalManifestAsync(
                choiceContext,
                phantom,
                cardPlay
            );
        }

        WoLiPower? woLi = creature.GetPower<WoLiPower>();
        if (woLi != null)
        {
            await woLi.RecordManifestAsync(
                choiceContext,
                phantom,
                isGroupExtra: false
            );
        }
    }

    public static Task NotifyGroupExtraManifestAsync(
        PlayerChoiceContext choiceContext,
        Player owner,
        AbstractLiDaoXuYing phantom
    )
    {
        WoLiPower? woLi = owner.Creature.GetPower<WoLiPower>();
        return woLi != null
            ? woLi.RecordManifestAsync(
                choiceContext,
                phantom,
                isGroupExtra: true
            )
            : Task.CompletedTask;
    }
}
