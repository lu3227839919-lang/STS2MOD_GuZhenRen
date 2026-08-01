using GuCard = global::GuZhenRen.Cards.AbstractGuZhenRenCard;

using GuZhenRen.Cards;

using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Powers;

/// <summary>
/// 光道道痕。
///
/// 没有闪耀时，每层使光道卡牌造成的正常攻击伤害提高 25%。
///
/// 拥有闪耀时，本能力不独立计算；
/// 闪耀会把光道道痕的倍率合并到自身倍率中，避免重复相乘。
/// </summary>
[RegisterPower]
public sealed class GuangDaoDaoHenPower
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

    public const decimal MultiplierPerStack =
        0.25m;

    public override decimal
        ModifyDamageMultiplicative(
            Creature? target,
            decimal amount,
            ValueProp props,
            Creature? dealer,
            CardModel? cardSource,
            CardPlay? cardPlay
        )
    {
        if (!ReferenceEquals(
                dealer,
                Owner
            ) ||
            !props.IsPoweredAttack() ||
            cardSource is not
                GuCard guCard ||
            guCard.CurrentDao !=
                GuCard.Dao
                    .GuangDao)
        {
            return 1m;
        }

        if (Owner.HasPower<ShanYaoPower>())
        {
            return 1m;
        }

        return 1m +
               Amount *
               MultiplierPerStack;
    }
}
