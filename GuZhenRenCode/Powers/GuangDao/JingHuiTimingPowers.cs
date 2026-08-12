using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Powers.GuangDao;

/// <summary>
/// 留光：本回合结束时仍有至少5点格挡，获得1点光辉，然后清除。
/// </summary>
[RegisterPower]
public sealed class JingHuiReservePower : ModPowerTemplate
{    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override bool IsVisibleInternal => false;

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants
    )
    {
        if (Amount <= 0 ||
            side != Owner.Side ||
            !participants.Contains(Owner))
        {
            return;
        }

        if (Owner.Block >= 5)
        {
            await GuangDaoPowerSystem.GainGuangHuiFromPower(
                choiceContext,
                Owner,
                1
            );
        }

        await PowerCmd.ModifyAmount(
            choiceContext,
            this,
            -Amount,
            Owner,
            cardSource: null
        );
    }
}

/// <summary>
/// 破镜：本次镜辉提供的防御窗口中，玩家格挡首次被完全击破时获得1光辉。
/// </summary>
[RegisterPower]
public sealed class JingHuiBreakPower : ModPowerTemplate
{    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override bool IsVisibleInternal => false;

    public override async Task AfterBlockBroken(
        PlayerChoiceContext choiceContext,
        Creature target,
        Creature? breaker
    )
    {
        if (Amount <= 0 || !ReferenceEquals(target, Owner))
        {
            return;
        }

        await GuangDaoPowerSystem.GainGuangHuiFromPower(
            choiceContext,
            Owner,
            1
        );
        await PowerCmd.ModifyAmount(
            choiceContext,
            this,
            -Amount,
            Owner,
            cardSource: null
        );
    }
}
