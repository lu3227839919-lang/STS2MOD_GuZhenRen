using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Powers;

/// <summary>
/// 偷道道痕。
///
/// 自身每次造成正常攻击伤害时获得等同于层数的金币。
/// 每场战斗至多通过该机制计算 30 金币。
///
/// 本移植版使用隐藏的 TouDaoGoldTrackerPower 保存战斗内累计值，
/// 不使用尖塔1的全局静态变量，因此支持多人和多实例。
/// </summary>
[RegisterPower]
public sealed class TouDaoDaoHenPower
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

    private const int MaximumGoldPerCombat =
        30;

    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource
    )
    {
        if (Amount <= 0 ||
            !ReferenceEquals(
                dealer,
                Owner
            ) ||
            ReferenceEquals(
                target,
                Owner
            ) ||
            !props.IsPoweredAttack())
        {
            return;
        }

        Player? player =
            Owner.Player;

        if (player == null)
        {
            return;
        }

        TouDaoGoldTrackerPower? tracker =
            Owner.GetPower<
                TouDaoGoldTrackerPower
            >();

        int stolenThisCombat =
            tracker?.Amount ?? 0;

        int remaining =
            MaximumGoldPerCombat -
            stolenThisCombat;

        if (remaining <= 0)
        {
            return;
        }

        int baseGoldToGain =
            Math.Min(
                Amount,
                remaining
            );

        Flash();

        // 必须走游戏命令，让金币修改、钩子和多人动作记录保持一致。
        // 上限仍按偷道的基础获得量计算。
        await PlayerCmd.GainGold(
            baseGoldToGain,
            player,
            false
        );

        // 上限按照偷道的基础获得量计算，
        // 与尖塔1 totalGoldStolenThisCombat 的语义一致。
        if (tracker == null)
        {
            await PowerCmd.Apply<
                TouDaoGoldTrackerPower
            >(
                choiceContext,
                Owner,
                baseGoldToGain,
                Owner,
                cardSource: null,
                silent: true
            );
        }
        else
        {
            await PowerCmd.ModifyAmount(
                choiceContext,
                tracker,
                baseGoldToGain,
                Owner,
                cardSource: null,
                silent: true
            );
        }
    }
}
