using Godot;

using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Characters;

/// <summary>
/// 蛊真人虚影专用卡池。
///
/// 该卡池用于让虚影进入 ModelDb、加载本地化和资源，
/// 不应加入角色的普通奖励卡池。
/// </summary>
public sealed class GuZhenRenXuYingCardPool
    : TypeListCardPoolModel
{
    /// <summary>
    /// 卡池的稳定标识。
    /// </summary>
    public override string Title =>
        "GuZhenRenXuYing";

    /// <summary>
    /// 与蛊真人普通卡共用能量颜色标识。
    ///
    /// 虚影使用负费用，不会显示普通能量图标；
    /// 这里保留路径仅作为通用 UI 的资源后备。
    /// </summary>
    public override string EnergyColorName =>
        "GuZhenRen";

    public override string? BigEnergyIconPath =>
        $"{Entry.ResPath}/images/characters/energy_big.png";

    public override string? TextEnergyIconPath =>
        $"{Entry.ResPath}/images/characters/energy_text.png";

    public override Color DeckEntryCardColor =>
        GuZhenRenCharacter.ThemeColor;

    public override Color EnergyOutlineColor =>
        new(0.08f, 0.18f, 0.24f);

    /// <summary>
    /// 与蛊真人普通卡共用青灰色卡框。
    /// </summary>
    public override Material? PoolFrameMaterial =>
        GD.Load<Material>(
            $"{Entry.ResPath}/materials/card_frame_cyan_gray.tres"
        );

    public override bool IsColorless =>
        false;
}
