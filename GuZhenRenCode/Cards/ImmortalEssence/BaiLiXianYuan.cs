using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.ImmortalEssence;

/// <summary>白荔仙元：8 个六转催动单位，等于两张红枣仙元。</summary>
public sealed class BaiLiXianYuan : AbstractXianYuanCard
{
    public override int ActivationUnits => 8;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://GuZhenRen/images/cards/BaiLiXianYuan.png"
    );
}
