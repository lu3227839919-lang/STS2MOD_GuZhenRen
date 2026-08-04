using GuZhenRen.Cards;
using GuZhenRen.Cards.GuangDao;

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
/// 聚光：下一张由持有者打出的光道攻击牌获得固定伤害加成，随后清空。
/// 作为内部状态隐藏，具体数值通过微光、聚光和余辉牌展示。
/// </summary>
[RegisterPower]
public sealed class JuGuangPower : ModPowerTemplate
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
               CanEmpower(cardSource)
            ? Amount
            : 0;
    }

    public override async Task AfterCardPlayed(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        if (Amount <= 0 ||
            cardPlay.PlayIndex != 0 ||
            !ReferenceEquals(cardPlay.Card.Owner, Owner.Player) ||
            !CanEmpower(cardPlay.Card))
        {
            return;
        }

        await PowerCmd.ModifyAmount(
            choiceContext,
            this,
            -Amount,
            Owner,
            cardPlay.Card
        );
    }
    private static bool CanEmpower(CardModel? card)
    {
        return card is YueGuangGu or YueRen or CanYue or ManYueRen;
    }

}
