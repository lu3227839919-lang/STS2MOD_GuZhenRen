using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.ImmortalEssence;

/// <summary>青提仙元：2 个六转催动单位。</summary>
public sealed class QingTiXianYuan : AbstractXianYuanCard
{
    public override int ActivationUnits => 2;

    public override CardAssetProfile AssetProfile =>
        global::GuZhenRen.Cards.CardImageCatalog.Create(GetType());
}
