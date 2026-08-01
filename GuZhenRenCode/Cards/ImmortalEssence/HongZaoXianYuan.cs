using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.ImmortalEssence;

/// <summary>红枣仙元：4 个六转催动单位，等于两张青提仙元。</summary>
public sealed class HongZaoXianYuan : AbstractXianYuanCard
{
    public override int ActivationUnits => 4;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://GuZhenRen/images/cards/HongZaoXianYuan.png"
    );
}
