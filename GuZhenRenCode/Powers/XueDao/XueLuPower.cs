using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Powers.XueDao;

/// <summary>
/// 血颅：最多三层的战斗内强化。每层强化血道寄生、血道防御与
/// 刀翅血蝠类多段攻击；资源产量不受影响。
/// </summary>
[RegisterPower]
public sealed class XueLuPower : ModPowerTemplate
{
    public const int MaximumAmount = 3;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://GuZhenRen/images/power/XueLuPower-64x64.png",
        BigIconPath: "res://GuZhenRen/images/power/XueLuPower-256x256.png"
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

        if (canonicalPower is not XueLuPower ||
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
