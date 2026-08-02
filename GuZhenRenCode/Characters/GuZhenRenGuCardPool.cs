using Godot;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Characters;

/// <summary>
/// Card pool for Gu Zhen Ren's Gu-insect cards.
/// Phantom cards and killer-move cards intentionally use their own pools.
/// </summary>
public sealed class GuZhenRenGuCardPool : TypeListCardPoolModel
{
    public override string Title => "GuZhenRenGu";
    public override string EnergyColorName => "GuZhenRen";

    public override string? BigEnergyIconPath =>
        $"{Entry.ResPath}/images/characters/energy_big.png";

    public override string? TextEnergyIconPath =>
        $"{Entry.ResPath}/images/characters/energy_text.png";

    public override Color DeckEntryCardColor =>
        GuZhenRenCharacter.ThemeColor;

    public override Color EnergyOutlineColor =>
        new(0.08f, 0.18f, 0.24f);

    public override Material? PoolFrameMaterial =>
        GD.Load<Material>(
            $"{Entry.ResPath}/materials/card_frame_ink_wash.tres"
        );

    public override bool IsColorless => false;
}
