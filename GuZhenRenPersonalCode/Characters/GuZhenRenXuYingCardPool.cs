using Godot;

using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Utils;

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
        Entry.ModId + "XuYing";

    /// <summary>
    /// 与蛊真人普通卡共用能量颜色标识。
    ///
    /// 虚影使用负费用，不会显示普通能量图标；
    /// 这里保留路径仅作为通用 UI 的资源后备。
    /// </summary>
    public override string EnergyColorName =>
        Entry.ModId;

    public override string? BigEnergyIconPath =>
        $"{Entry.ResPath}/images/characters/energy_big.png";

    public override string? TextEnergyIconPath =>
        $"{Entry.ResPath}/images/characters/energy_text.png";

    public override Color DeckEntryCardColor => new(0.88f, 0.88f, 0.88f);

    public override Color EnergyOutlineColor =>
        new(0.08f, 0.18f, 0.24f);

    /// <summary>
    /// 虚影卡框与普通卡统一使用同一 HSV 卡框材质（h=0.603, s=0.19, v=1.2）。
    /// </summary>
    private static readonly Material? _poolFrameMaterial =
        MaterialUtils.CreateHsvShaderMaterial(0.56f, 0.19f, 1.2f);
    public override Material? PoolFrameMaterial => _poolFrameMaterial;

    public override bool IsColorless =>
        false;
}
