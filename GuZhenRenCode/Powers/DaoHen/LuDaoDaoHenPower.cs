using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using GuZhenRen.Multiplayer;

using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Powers;

/// <summary>
/// 律道道痕。
///
/// 玩家回合结束时，使所有敌人在下一次敌方回合中
/// 暂时失去等同于层数的力量。
///
/// 若敌人拥有人工制品，负力量会被人工制品阻挡，
/// 且不会施加恢复力量用的“镣铐”能力。
/// </summary>
[RegisterPower]
public sealed class LuDaoDaoHenPower
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

        Creature[] targets =
            GuZhenRenDeterminism.OrderCreatures(
                CombatState.HittableEnemies
            );

        if (targets.Length == 0)
        {
            return;
        }

        Flash();

        foreach (Creature target in targets)
        {
            // 与尖塔1原逻辑一致：
            // 在负力量结算前记录是否存在人工制品。
            bool hadArtifact =
                target.GetPower<ArtifactPower>() != null;

            await PowerCmd.Apply<StrengthPower>(
                choiceContext,
                target,
                -Amount,
                Owner,
                cardSource: null
            );

            if (hadArtifact)
            {
                continue;
            }

            // 敌方回合结束后恢复刚才失去的力量。
            await PowerCmd.Apply<
                LuDaoRestoreStrengthPower
            >(
                choiceContext,
                target,
                Amount,
                Owner,
                cardSource: null
            );
        }
    }
}
