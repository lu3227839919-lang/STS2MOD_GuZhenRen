using System.Linq;

using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Powers.XueDao;

/// <summary>
/// 流血：持有者回合结束时移除一层，施加者获得一点血元，
/// 然后持有者受到三点伤害。
/// </summary>
[RegisterPower]
public sealed class LiuXuePower : ModPowerTemplate
{
    public const int DamagePerTick = 3;

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerInstanceType InstanceType =>
        PowerInstanceType.InstancedPerApplier;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://GuZhenRen//images//power//LiuXuePower-64x64.png",
        BigIconPath: "res://GuZhenRen/images/power/LiuXuePower-256x256.png"
    );

    public override async Task BeforeSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants
    )
    {
        if (Amount <= 0 || !participants.Contains(Owner))
        {
            return;
        }

        Creature target = Owner;
        Creature? applier = Applier;
        Flash();

        await PowerCmd.Decrement(this);
        await XueDaoPowerSystem.GainXueYuanFromEffect(
            choiceContext,
            applier,
            1
        );

        if (!target.IsDead)
        {
            await CreatureCmd.Damage(
                choiceContext,
                target,
                DamagePerTick,
                ValueProp.Unpowered,
                dealer: null,
                cardSource: null,
                cardPlay: null
            );
        }
    }
}
