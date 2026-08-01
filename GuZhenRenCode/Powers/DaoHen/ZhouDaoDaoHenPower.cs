using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;

using GuZhenRen.Multiplayer;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2RitsuLib;

namespace GuZhenRen.Powers;

/// <summary>
/// 宙道道痕。
///
/// 玩家回合结束时，按照当前玩家回合数重复攻击随机敌人。
///
/// 每次造成等同于宙道道痕层数的被动伤害：
///
/// - 第 1 回合结束：1 次；
/// - 第 2 回合结束：2 次；
/// - 第 3 回合结束：3 次。
/// </summary>
[RegisterPower]
public sealed class ZhouDaoDaoHenPower
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

        int turnCount =
            Owner.Player?
                .PlayerCombatState?
                .TurnNumber ??
            0;

        if (turnCount <= 0)
        {
            return;
        }

        Flash();

        for (int hit = 0;
             hit < turnCount;
             hit++)
        {
            Creature[] targets =
                GuZhenRenDeterminism.OrderCreatures(
                    CombatState.HittableEnemies
                );

            if (targets.Length == 0)
            {
                break;
            }

            // 道痕拥有者是 Creature；跑局 RNG 位于玩家对象。
            var ownerPlayer =
                Owner.Player;

            if (ownerPlayer == null)
            {
                break;
            }

            int targetIndex = RitsuLibFramework
                .GetModPlayerRng(
                    ownerPlayer,
                    Entry.ModId,
                    "dao_hen/zhou/target"
                )
                .NextInt(targets.Length);

            Creature target =
                targets[targetIndex];

            await CreatureCmd.Damage(
                choiceContext,
                target,
                Amount,
                ValueProp.Unpowered |
                    ValueProp.SkipHurtAnim,
                Owner
            );
        }
    }
}
