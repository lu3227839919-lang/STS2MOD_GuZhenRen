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
/// 照破：敌方每有一层，下一次受到攻击牌的单段伤害时额外受到 3 点伤害，
/// 随后移除一层。多段攻击会逐段重复读取并消耗层数。
/// </summary>
[RegisterPower]
public sealed class ZhaoPoPower : ModPowerTemplate
{
    public const int DamagePerLayer = 3;

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override decimal ModifyDamageAdditive(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay
    )
    {
        return ReferenceEquals(target, Owner) &&
            Amount > 0 &&
            cardSource?.Type == CardType.Attack &&
            ZhaoPoTriggerScope.CanTrigger
                ? DamagePerLayer
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
        if (!ReferenceEquals(target, Owner) ||
            Amount <= 0 ||
            cardSource?.Type != CardType.Attack ||
            !ZhaoPoTriggerScope.CanTrigger)
        {
            return;
        }

        Flash();
        await PowerCmd.Decrement(this);
    }
}
