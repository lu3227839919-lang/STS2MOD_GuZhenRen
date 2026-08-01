using MegaCrit.Sts2.Core.Entities.Powers;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Powers;

/// <summary>
/// 日光。
///
/// 本能力本身不直接修改数值。
/// 玩家使用光道攻击牌时，闪耀会优先消耗 1 层日光；
/// 没有日光时，闪耀会被移除。
/// </summary>
[RegisterPower]
public sealed class RiGuangPower
    : ModPowerTemplate
{
    public override PowerType Type =>
        PowerType.Buff;

    public override PowerStackType StackType =>
        PowerStackType.Counter;

    /// <summary>
    /// Power 图标资源。
    ///
    /// 实际 Godot 地址：
    /// res://GuZhenRen/images/powers/RiGuangPower.png
    /// res://GuZhenRen/images/powers/RiGuangPower_p.png
    /// </summary>
    public override PowerAssetProfile AssetProfile =>
        new(
            IconPath:
                $"{Entry.ResPath}/images/powers/RiGuangpower_p-64x64.png",
            BigIconPath:
                $"{Entry.ResPath}/images/powers/RiGuangpower_p-256x256.png"
        );
    /// <summary>
    /// 日光被闪耀消耗时播放能力闪烁。
    /// </summary>
    internal void FlashForConsumption()
    {
        Flash();
    }

}
