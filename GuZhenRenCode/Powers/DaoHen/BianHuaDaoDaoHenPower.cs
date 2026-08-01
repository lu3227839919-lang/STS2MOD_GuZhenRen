using System;
using System.Threading.Tasks;

using GuCard = global::GuZhenRen.Cards.AbstractGuZhenRenCard;

using GuZhenRen.Cards;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Powers;

/// <summary>
/// 变化道道痕。
///
/// 玩家打出蛊真人体系卡牌后，
/// 根据该牌的 CurrentDao 转化为同层对应道痕。
/// </summary>
[RegisterPower]
public sealed class BianHuaDaoDaoHenPower
    : AbstractDaoHenPower
{

    /// <summary>
    /// 当前能力使用的图标资源。
    /// </summary>
    public override PowerAssetProfile AssetProfile =>
        new(
            IconPath:
                $"{Entry.ResPath}/images/powers/{GetType().Name}_p-64x64.png",
            BigIconPath:
                $"{Entry.ResPath}/images/powers/{GetType().Name}_p-256x256.png"
        );

/// <summary>
    /// 出牌完成后进行转化。
    /// </summary>
    public override async Task AfterCardPlayed(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        ArgumentNullException.ThrowIfNull(
            cardPlay
        );

        if (!cardPlay.IsFirstInSeries)
        {
            return;
        }

        if (cardPlay.Card is not
            GuCard guCard)
        {
            return;
        }

        if (!ReferenceEquals(
                guCard.Owner.Creature,
                Owner
            ))
        {
            return;
        }

        GuCard.Dao? dao =
            guCard.CurrentDao;

        if (dao == null ||
            dao ==
                GuCard.Dao
                    .BianHuaDao)
        {
            return;
        }

        int amount = Amount;
        Creature owner = Owner;

        if (amount <= 0)
        {
            return;
        }

        // 先判断当前批次是否已经移植了对应道痕。
        bool supported =
            await DaoHenPowerRegistry
                .TryApplyAsync(
                    choiceContext,
                    dao.Value,
                    owner,
                    amount
                );

        if (!supported)
        {
            return;
        }

        Flash();

        // 新道痕已经成功施加，再移除变化道。
        await PowerCmd.Remove(this);

        await ZhuanYiPower.TriggerAsync(
            choiceContext,
            owner
        );
    }
}
