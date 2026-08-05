using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Powers.XueDao;

/// <summary>
/// 血河余甲：在玩家下一回合开始时获得记录的格挡，然后清除。
/// </summary>
[RegisterPower]
public sealed class XueHeJiaCarryPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override bool IsVisibleInternal => false;

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState
    )
    {
        if (Amount <= 0 ||
            side != Owner.Side ||
            !participants.Contains(Owner))
        {
            return;
        }

        int block = Amount;
        await PowerCmd.Remove(this);
        await CreatureCmd.GainBlock(
            Owner,
            block,
            MegaCrit.Sts2.Core.ValueProps.ValueProp.Unpowered,
            cardPlay: null
        );
    }
}
