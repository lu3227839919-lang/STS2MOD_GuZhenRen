using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.ImmortalEssence;

/// <summary>白荔仙元：获得 4 点能量。</summary>
public sealed class BaiLiXianYuan : AbstractXianYuanCard
{
    protected override int EnergyGain => 4;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://GuZhenRen/images/cards/BaiLiXianYuan.png"
    );
}
