using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Powers.XueDao;

/// <summary>
/// 血元：血道蛊牌专属的角色资源，最多保留十二点。
/// </summary>
[RegisterPower]
public sealed class XueYuanPower : ModPowerTemplate
{
    public const int MaximumAmount = 12;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    // 暂时复用现有资源图标，避免新增机制依赖尚未提供的美术文件。
    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://GuZhenRenPersonal//images//power//XueYuanPower-64x64.png",
        BigIconPath: "res://GuZhenRenPersonal/images/power/XueYuanPower-256x256.png"
    );

    public override bool TryModifyPowerAmountReceived(
        PowerModel canonicalPower,
        Creature target,
        decimal amount,
        Creature? applier,
        out decimal modifiedAmount
    )
    {
        modifiedAmount = amount;

        if (canonicalPower is not XueYuanPower ||
            !ReferenceEquals(target, Owner) ||
            amount <= 0)
        {
            return false;
        }

        modifiedAmount = Math.Min(
            amount,
            Math.Max(0, MaximumAmount - Amount)
        );
        return modifiedAmount != amount;
    }
}
