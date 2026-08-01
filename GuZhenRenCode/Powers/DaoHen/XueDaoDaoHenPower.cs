using System;
using System.Threading.Tasks;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Powers;

/// <summary>
/// 血道道痕。
///
/// 自身使用正常攻击造成未格挡伤害后，
/// 每层按 1% 的比例治疗，结果向上取整。
/// </summary>
[RegisterPower]
public sealed class XueDaoDaoHenPower
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

private const decimal LifestealPerStack =
        0.01m;

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
            !ReferenceEquals(dealer, Owner) ||
            ReferenceEquals(target, Owner) ||
            result.UnblockedDamage <= 0 ||
            !props.IsPoweredAttack())
        {
            return;
        }

        int healAmount =
            (int)Math.Ceiling(
                result.UnblockedDamage *
                Amount *
                LifestealPerStack
            );

        if (healAmount <= 0)
        {
            return;
        }

        Flash();

        await CreatureCmd.Heal(
            Owner,
            healAmount
        );
    }
}
