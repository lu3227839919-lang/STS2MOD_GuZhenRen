using Godot;

using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Characters;

/// <summary>
/// 仙元牌专用隐藏卡池。
/// 只用于模型注册和战斗内生成，不加入普通卡牌奖励。
/// </summary>
public sealed class GuZhenRenXianYuanCardPool : TypeListCardPoolModel
{
    public override string Title => "GuZhenRenXianYuan";

    public override string EnergyColorName => "GuZhenRen";

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
