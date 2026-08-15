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
/// 闪耀 X：下一张由持有者打出的光道攻击牌第一段伤害提高 X 点，
/// 随后清空。保留旧类名以兼容既有存档与多人快照中的 Power ID。
/// </summary>
[RegisterPower]
public sealed class JuGuangPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath:
            "res://GuZhenRenPersonal/images/power/ShanYaoPower-64x64.png",
        BigIconPath:
            "res://GuZhenRenPersonal/images/power/ShanYaoPower-256x256.png"
    );

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
            !CanEmpower(cardSource))
        {
            return;
        }

        // 固定增伤只作用于下一张光道攻击牌的第一段伤害，
        // 防止月芒等多段攻击按段数重复放大闪耀收益。
        await PowerCmd.ModifyAmount(
            choiceContext,
            this,
            -Amount,
            Owner,
            cardSource
        );
    }
    private static bool CanEmpower(CardModel? card)
    {
        return card?.Type == CardType.Attack &&
               GuangDaoPowerSystem.IsGuangDaoCard(card);
    }

}
