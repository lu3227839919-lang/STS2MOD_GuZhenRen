using Godot;

using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Utils;

namespace GuZhenRen.Characters;

/// <summary>
/// 仙元牌专用隐藏卡池。
/// 只用于模型注册和战斗内生成，不加入普通卡牌奖励。
/// </summary>
public sealed class GuZhenRenXianYuanCardPool : TypeListCardPoolModel
{
    public override string Title => Entry.ModId + "XianYuan";

    public override string EnergyColorName => Entry.ModId;

    public override string? BigEnergyIconPath =>
        $"{Entry.ResPath}/images/characters/energy_big.png";

    public override string? TextEnergyIconPath =>
        $"{Entry.ResPath}/images/characters/energy_text.png";

    public override Color DeckEntryCardColor => new(0.88f, 0.88f, 0.88f);

    public override Color EnergyOutlineColor =>
        new(0.08f, 0.18f, 0.24f);

    // 卡框颜色由 HSV 材质控制（h=0.603, s=0.19, v=1.2）。
    private static readonly Material? _poolFrameMaterial =
        MaterialUtils.CreateHsvShaderMaterial(0.603f, 0.19f, 1.2f);
    public override Material? PoolFrameMaterial => _poolFrameMaterial;

    public override bool IsColorless => false;
}
