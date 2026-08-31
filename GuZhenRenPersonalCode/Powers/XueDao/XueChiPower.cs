using MegaCrit.Sts2.Core.Entities.Powers;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Powers.XueDao;

/// <summary>血池跨回合保留，等待下一张成功触发血寄效果的主动出牌。</summary>
[RegisterPower]
public sealed class XueChiPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://GuZhenRenPersonal/images/power/XueJiPower-64x64.png",
        BigIconPath: "res://GuZhenRenPersonal/images/power/XueJiPower-256x256.png"
    );
}
