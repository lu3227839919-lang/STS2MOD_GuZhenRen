using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Powers;

/// <summary>
/// 镣铐。
///
/// 律道道痕临时降低敌人力量后施加。
/// 该敌人参与的敌方回合结束时，恢复对应力量并移除自身。
/// </summary>
[RegisterPower]
public sealed class LuDaoRestoreStrengthPower
    : ModPowerTemplate
{
    public override PowerType Type =>
        PowerType.Debuff;

    public override PowerStackType StackType =>
        PowerStackType.Counter;

    /// <summary>
    /// 复用律道道痕图标，不需要额外准备一套图标。
    /// </summary>
    public override PowerAssetProfile AssetProfile =>
        new(
            IconPath:
                $"{Entry.ResPath}/images/powers/LuDaoDaoHenPower_p-64x64.png",
            BigIconPath:
                $"{Entry.ResPath}/images/powers/LuDaoDaoHenPower_p-256x256.png"
        );

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants
    )
    {
        if (side != CombatSide.Enemy ||
            Amount <= 0 ||
            !participants.Contains(Owner))
        {
            return;
        }

        int amount =
            Amount;

        Flash();

        await PowerCmd.Apply<StrengthPower>(
            choiceContext,
            Owner,
            amount,
            Applier,
            cardSource: null
        );

        await PowerCmd.Remove(
            this
        );
    }
}
