using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Powers.XueDao;

/// <summary>
/// 万骸归潮的六转质变：本回合消耗满 3 张遗骸时，
/// 下回合开始时获得本次杀招格挡值的一半，然后移除。
/// </summary>
[RegisterPower]
public sealed class WanHaiGuiChaoRetainBlockPower
    : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType =>
        PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath:
            "res://GuZhenRen//images//power//WanHaiGuiChaoRetainBlockPower-64x64.png",
        BigIconPath:
            "res://GuZhenRen//images//power//WanHaiGuiChaoRetainBlockPower-256x256.png"
    );

    public override async Task AfterEnergyReset(Player player)
    {
        if (!ReferenceEquals(player, Owner.Player))
        {
            return;
        }

        Flash();
        await CreatureCmd.GainBlock(
            Owner,
            Amount,
            ValueProp.Unpowered | ValueProp.Move,
            null
        );
        await PowerCmd.Remove(this);
    }
}
