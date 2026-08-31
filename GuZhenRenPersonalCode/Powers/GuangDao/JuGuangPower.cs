using MegaCrit.Sts2.Core.Entities.Powers;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Powers.GuangDao;

/// <summary>
/// 聚光层数。它不监听伤害；统一折光系统只在下一张具有折光效果的牌
/// 真正触发折光时消费一层，并把该牌的折光效果结算次数从一变为二。
/// </summary>
[RegisterPower]
public sealed class JuGuangPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath:
            "res://GuZhenRenPersonal/images/power/ShanYaoPower-64x64.png",
        BigIconPath:
            "res://GuZhenRenPersonal/images/power/ShanYaoPower-256x256.png"
    );
}
