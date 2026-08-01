using MegaCrit.Sts2.Core.Entities.Powers;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Powers;

/// <summary>
/// 偷道本场战斗金币计数器。
///
/// 该能力不可见，Amount 表示本场战斗已经通过偷道获得的金币。
/// 道痕在不同流派之间转化时，本计数器仍会保留；
/// 战斗结束时会随普通战斗能力一同清除。
/// </summary>
[RegisterPower]
public sealed class TouDaoGoldTrackerPower
    : ModPowerTemplate
{
    public override PowerType Type =>
        PowerType.Buff;

    public override PowerStackType StackType =>
        PowerStackType.Counter;

    protected override bool IsVisibleInternal =>
        false;

    public override bool ShouldPlayVfx =>
        false;

    /// <summary>
    /// Power 图标资源。
    ///
    /// 实际 Godot 地址：
    /// res://GuZhenRen/images/powers/TouDaoGoldTrackerPower.png
    /// res://GuZhenRen/images/powers/TouDaoGoldTrackerPower_p.png
    /// </summary>
    public override PowerAssetProfile AssetProfile =>
        new(
            IconPath:
                $"{Entry.ResPath}/images/powers/TouDaoDaoHenPower_p-64x64.png",
            BigIconPath:
                $"{Entry.ResPath}/images/powers/TouDaoDaoHenPower_p-256x256.png"
        );
}
