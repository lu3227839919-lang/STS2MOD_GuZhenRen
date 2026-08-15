using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Powers.XueDao;

/// <summary>
/// 血河余甲：在玩家下一回合清除本回合格挡后获得记录的格挡，然后清除。
/// </summary>
[RegisterPower]
public sealed class XueHeJiaCarryPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override bool IsVisibleInternal => false;

    public override async Task AfterBlockCleared(Creature creature)
    {
        if (Amount <= 0 || !ReferenceEquals(creature, Owner))
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
