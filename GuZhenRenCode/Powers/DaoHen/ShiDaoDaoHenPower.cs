using System;
using System.Threading.Tasks;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Powers;

/// <summary>
/// 食道道痕。
///
/// 自身通过正常攻击击杀一个非仆从敌人时，
/// 回复等同于食道道痕层数的生命。
/// </summary>
[RegisterPower]
public sealed class ShiDaoDaoHenPower
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
            !target.IsMonster ||
            !props.IsPoweredAttack() ||
            !result.WasTargetKilled)
        {
            return;
        }

        // 原版食道不会因击杀仆从而治疗。
        if (target.GetPower<MinionPower>() != null)
        {
            return;
        }

        Flash();

        await CreatureCmd.Heal(
            Owner,
            Amount
        );
    }
}
