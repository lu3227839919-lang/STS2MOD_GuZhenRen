using System;
using System.Linq;
using System.Threading.Tasks;

using GuZhenRen.Multiplayer;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Powers;

/// <summary>
/// 风道道痕。
///
/// 每抽到一张自己的牌，对所有敌人造成等同于层数的被动伤害。
/// </summary>
[RegisterPower]
public sealed class FengDaoDaoHenPower
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

public override async Task AfterCardDrawn(
        PlayerChoiceContext choiceContext,
        CardModel card,
        bool fromHandDraw
    )
    {
        if (Amount <= 0 ||
            !ReferenceEquals(
                card.Owner.Creature,
                Owner
            ))
        {
            return;
        }

        Creature[] targets =
            GuZhenRenDeterminism.OrderCreatures(
                CombatState.HittableEnemies
            );

        if (targets.Length == 0)
        {
            return;
        }

        Flash();

        await CreatureCmd.Damage(
            choiceContext,
            targets,
            Amount,
            ValueProp.Unpowered |
                ValueProp.SkipHurtAnim,
            Owner
        );
    }
}
