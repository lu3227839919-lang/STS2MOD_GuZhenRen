using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.ImmortalEssence;

/// <summary>
/// 黄杏仙元：16 个六转催动单位，等于两张白荔仙元。
/// </summary>
public sealed class HuangXingXianYuan : AbstractXianYuanCard
{
    public override int ActivationUnits => 16;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://GuZhenRen/images/cards/HuangXingXianYuan.png"
    );

}
