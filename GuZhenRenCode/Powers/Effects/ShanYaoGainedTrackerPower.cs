using MegaCrit.Sts2.Core.Entities.Powers;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Powers;

/// <summary>
/// 本场战斗累计获得的闪耀层数。
///
/// 用于“三十三天光”等效果读取。
/// 该能力不可见，并且不会随着当前闪耀被消耗而减少。
/// </summary>
[RegisterPower]
public sealed class ShanYaoGainedTrackerPower
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
    /// res://GuZhenRen/images/powers/ShanYaoGainedTrackerPower.png
    /// res://GuZhenRen/images/powers/ShanYaoGainedTrackerPower_p.png
    /// </summary>
    public override PowerAssetProfile AssetProfile =>
        new(
            IconPath:
                $"{Entry.ResPath}/images/powers/{GetType().Name}_p-64x64.png",
            BigIconPath:
                $"{Entry.ResPath}/images/powers/{GetType().Name}_p-256x256.png"
        );
}
