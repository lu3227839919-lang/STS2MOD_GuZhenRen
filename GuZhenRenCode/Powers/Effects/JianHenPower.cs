using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using GuCard = global::GuZhenRen.Cards.AbstractGuZhenRenCard;

using GuZhenRen.Cards;

using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Powers;

/// <summary>
/// 剑痕。
///
/// 目标受到剑道卡牌的正常攻击伤害时，
/// 额外受到等同于剑痕层数的伤害。
///
/// 敌方回合结束后移除。
/// </summary>
[RegisterPower]
public sealed class JianHenPower
    : ModPowerTemplate
{
    public override PowerType Type =>
        PowerType.Debuff;

    public override PowerStackType StackType =>
        PowerStackType.Counter;

    /// <summary>
    /// Power 图标资源。
    ///
    /// 实际 Godot 地址：
    /// res://GuZhenRen/images/powers/JianHenPower.png
    /// res://GuZhenRen/images/powers/JianHenPower_p.png
    /// </summary>
    public override PowerAssetProfile AssetProfile =>
        new(
            IconPath:
                $"{Entry.ResPath}/images/powers/{GetType().Name}_p-64x64.png",
            BigIconPath:
                $"{Entry.ResPath}/images/powers/{GetType().Name}_p-256x256.png"
        );

    /// <summary>
    /// 给剑道卡牌造成的伤害增加固定数值。
    /// </summary>
    public override decimal ModifyDamageAdditive(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay
    )
    {
        if (!ReferenceEquals(
                target,
                Owner
            ) ||
            !props.IsPoweredAttack() ||
            cardSource is not
                GuCard guCard ||
            guCard.CurrentDao !=
                GuCard.Dao
                    .JianDao)
        {
            return 0m;
        }

        return Amount;
    }

    /// <summary>
    /// 在目标参与的敌方回合结束后移除剑痕。
    /// </summary>
    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants
    )
    {
        if (side != CombatSide.Enemy ||
            !participants.Contains(Owner))
        {
            return;
        }

        await PowerCmd.Remove(
            this
        );
    }
}
