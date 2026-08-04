using GuZhenRen.Cards;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Powers.GuangDao;

/// <summary>
/// 定光：下一张由持有者打出的攻击牌，其第一段若命中带有照破的敌人，
/// 获得固定伤害加成。无论是否满足条件，第一次攻击伤害结算后都会清空，
/// 避免多段攻击把固定增益按段数放大。
/// </summary>
[RegisterPower]
public sealed class DingGuangChargePower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override bool IsVisibleInternal => false;

    public override decimal ModifyDamageAdditive(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay
    )
    {
        return Amount > 0 &&
               ReferenceEquals(dealer, Owner) &&
               cardSource?.Type == CardType.Attack &&
               target?.GetPower<ZhaoPoPower>() is { Amount: > 0 }
            ? Amount
            : 0;
    }

    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource
    )
    {
        if (Amount <= 0 ||
            !ReferenceEquals(dealer, Owner) ||
            cardSource?.Type != CardType.Attack)
        {
            return;
        }

        await PowerCmd.ModifyAmount(
            choiceContext,
            this,
            -Amount,
            Owner,
            cardSource
        );
    }
}
