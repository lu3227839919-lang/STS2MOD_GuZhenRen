using System;

using MegaCrit.Sts2.Core.Entities.Creatures;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Powers;

/// <summary>
/// 木道道痕。
///
/// 每层使自身受到的正数治疗提高 15%。
///
/// 计算完成后按照尖塔1的 MathUtils.round 语义，
/// 使用 MidpointRounding.AwayFromZero 四舍五入为整数。
/// </summary>
[RegisterPower]
public sealed class MuDaoDaoHenPower
    : AbstractDaoHenPower,
      IGuZhenRenHealAmountModifier
{

    /// <summary>
    /// 当前能力使用的图标资源。
    /// </summary>
    public override PowerAssetProfile AssetProfile =>
        new(
            IconPath:
                $"{Entry.ResPath}/images/powers/{GetType().Name}_p-64x64.png",
            BigIconPath:
                $"{Entry.ResPath}/images/powers/{GetType().Name}_p-256x256.png"
        );

private const decimal HealMultiplierPerStack =
        0.15m;

    /// <summary>
    /// 修改所属生物接受的治疗量。
    /// </summary>
    public decimal ModifyHealAmount(
        Creature creature,
        decimal amount
    )
    {
        if (amount <= 0m ||
            !ReferenceEquals(
                creature,
                Owner
            ) ||
            Amount <= 0)
        {
            return amount;
        }

        Flash();

        decimal multiplier =
            1m +
            Amount *
            HealMultiplierPerStack;

        return decimal.Round(
            amount * multiplier,
            decimals: 0,
            mode:
                MidpointRounding
                    .AwayFromZero
        );
    }
}
