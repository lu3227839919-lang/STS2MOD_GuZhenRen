using System.Threading.Tasks;

using GuCard = global::GuZhenRen.Cards.AbstractGuZhenRenCard;

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

namespace GuZhenRen.Powers;

/// <summary>
/// 剑锋。
///
/// 当所属玩家通过剑道卡牌造成正常攻击伤害时，
/// 给受击目标施加等同于剑锋层数的剑痕。
///
/// 尖塔2伤害结果会保留 cardSource，
/// 因此不需要尖塔1中的“出牌期间开关标记”。
/// </summary>
[RegisterPower]
public sealed class JianFengPower
    : ModPowerTemplate
{
    public override PowerType Type =>
        PowerType.Buff;

    public override PowerStackType StackType =>
        PowerStackType.Counter;

    /// <summary>
    /// Power 图标资源。
    ///
    /// 实际 Godot 地址：
    /// res://GuZhenRen/images/powers/JianFengPower.png
    /// res://GuZhenRen/images/powers/JianFengPower_p.png
    /// </summary>
    public override PowerAssetProfile AssetProfile =>
        new(
            IconPath:
                $"{Entry.ResPath}/images/powers/{GetType().Name}_p-64x64.png",
            BigIconPath:
                $"{Entry.ResPath}/images/powers/{GetType().Name}_p-256x256.png"
        );

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
            !props.IsPoweredAttack() ||
            cardSource is not
                GuCard guCard ||
            guCard.CurrentDao !=
                GuCard.Dao
                    .JianDao)
        {
            return;
        }

        Flash();

        await PowerCmd.Apply<JianHenPower>(
            choiceContext,
            target,
            Amount,
            Owner,
            cardSource
        );
    }
}
