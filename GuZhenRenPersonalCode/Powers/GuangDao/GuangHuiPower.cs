using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Powers.GuangDao;

/// <summary>
/// 光辉：上限 9 点、不会在回合结束时消失的角色资源。
/// </summary>
[RegisterPower]
public sealed class GuangHuiPower : ModPowerTemplate
{
    public const int MaximumAmount = 9;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://GuZhenRenPersonal//images//power//GuangHuiPower-64x64.png",
        BigIconPath: "res://GuZhenRenPersonal/images/power/GuangHuiPower-256x256.png"
    );
    
    /// <summary>
    /// 已存在的光辉在收到后续正向叠加时再次执行硬上限检查。
    /// 初次施加的上限由空窍遗物和 GuangDaoPowerSystem 双重保证。
    /// </summary>
    public override bool TryModifyPowerAmountReceived(
        PowerModel canonicalPower,
        Creature target,
        decimal amount,
        Creature? applier,
        out decimal modifiedAmount
    )
    {
        modifiedAmount = amount;

        if (canonicalPower is not GuangHuiPower ||
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
