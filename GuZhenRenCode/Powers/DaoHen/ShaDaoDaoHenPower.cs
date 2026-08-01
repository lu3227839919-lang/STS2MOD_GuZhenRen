using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using GuZhenRen.Multiplayer;

using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Powers;

/// <summary>
/// 杀道道痕。
///
/// 玩家回合结束前，对所有敌人造成每层 3 点被动伤害。
/// </summary>
[RegisterPower]
public sealed class ShaDaoDaoHenPower
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

private const int DamagePerStack = 3;

    public override async Task BeforeSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants
    )
    {
        if (side != CombatSide.Player ||
            Amount <= 0 ||
            !participants.Contains(Owner))
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
            Amount * DamagePerStack,
            ValueProp.Unpowered |
                ValueProp.SkipHurtAnim,
            Owner
        );
    }
}
