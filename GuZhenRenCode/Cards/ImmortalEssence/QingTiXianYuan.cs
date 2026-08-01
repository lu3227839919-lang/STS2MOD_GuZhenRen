using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.ImmortalEssence;

/// <summary>青提仙元：获得 2 点能量。</summary>
public sealed class QingTiXianYuan : AbstractXianYuanCard
{
    protected override int EnergyGain => 2;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://GuZhenRen/images/cards/QingTiXianYuan.png"
    );
}
