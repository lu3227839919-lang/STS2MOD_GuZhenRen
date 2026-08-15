using Godot;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Characters;

/// <summary>
/// Card pool for Gu Zhen Ren's ordinary Gu-insect and HeLian Gu cards.
/// Combat-generated companions and derivative cards live in the auxiliary
/// GuZhenRenCardPool instead, while phantom, immortal-essence, and killer-move
/// cards keep their own specialized pools.
/// </summary>
public sealed class GuZhenRenGuCardPool : TypeListCardPoolModel
{
    public override string Title => Entry.ModId + "Gu";
    public override string EnergyColorName => Entry.ModId;

    public override string? BigEnergyIconPath =>
        $"{Entry.ResPath}/images/characters/energy_big.png";

    public override string? TextEnergyIconPath =>
        $"{Entry.ResPath}/images/characters/energy_text.png";

    public override Color DeckEntryCardColor =>
        GuZhenRenCardVisualStyle.CardBackgroundColor;

    public override Color EnergyOutlineColor =>
        new(0.08f, 0.18f, 0.24f);

    public override Material? PoolFrameMaterial =>
        GuZhenRenCardVisualStyle.FrameMaterial;

    public override bool IsColorless => false;
}
