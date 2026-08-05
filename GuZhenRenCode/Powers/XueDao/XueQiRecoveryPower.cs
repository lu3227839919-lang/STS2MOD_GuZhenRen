using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Powers.XueDao;

/// <summary>
/// 血气蛊的延迟回复状态：在持有者下回合重置能量后回复生命并移除。
/// </summary>
[RegisterPower]
public sealed class XueQiRecoveryPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://GuZhenRen//images//power//XueQiRecoveryPower-64x64.png",
        BigIconPath: "res://GuZhenRen/images/power/XueQiRecoveryPower-256x256.png"
    );

    public override async Task AfterEnergyReset(Player player)
    {
        if (!ReferenceEquals(player, Owner.Player))
        {
            return;
        }

        Flash();
        await CreatureCmd.Heal(Owner, Amount);
        await PowerCmd.Remove(this);
    }
}
