using MegaCrit.Sts2.Core.Entities.Powers;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Powers;

/// <summary>
/// 小光蛊提供的闪耀额外作用次数。
///
/// 本能力不可见。
///
/// 每层使当前闪耀在作用于一张光道攻击牌后不会被移除；
/// 成功保护一次闪耀后消耗一层。
/// </summary>
[RegisterPower]
public sealed class XiaoGuangGuShanYaoUsesPower
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

    public override PowerAssetProfile AssetProfile =>
        new(
            IconPath:
                $"{Entry.ResPath}/images/powers/ShanYaoGainedTrackerPower_p-64x64.png",
            BigIconPath:
                $"{Entry.ResPath}/images/powers/ShanYaoGainedTrackerPower_p-256x256.png"
        );

    /// <summary>
    /// 成功消耗一次额外作用次数时播放闪烁。
    /// </summary>
    internal void FlashForConsumption()
    {
        Flash();
    }
}
