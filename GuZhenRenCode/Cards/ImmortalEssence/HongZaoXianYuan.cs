using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.ImmortalEssence;

/// <summary>红枣仙元：获得 3 点能量。</summary>
public sealed class HongZaoXianYuan : AbstractXianYuanCard
{
    protected override int EnergyGain => 3;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://GuZhenRen/images/cards/HongZaoXianYuan.png"
    );
}
