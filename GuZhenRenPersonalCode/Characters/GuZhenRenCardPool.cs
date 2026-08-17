using Godot;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Utils;

namespace GuZhenRen.Characters;

public sealed class GuZhenRenCardPool : TypeListCardPoolModel
{
    /// <summary>
    /// Auxiliary Gu Zhen Ren card pool for starter, companion, and derivative
    /// cards. Generated cards are registered here so the ordinary Gu pool can
    /// contain only real Gu-insects and HeLian results; phantom, immortal-
    /// essence, and killer-move cards keep their own specialized pools.
    /// </summary>
    // Title 和 EnergyColorName 是池子的稳定标识，不是玩家看到的角色名。
    // 自定义角色卡、遗物、药水池保持同一个 EnergyColorName，方便实验室和文本统一读取能量图标。
    public override string Title => Entry.ModId;
    public override string EnergyColorName => Entry.ModId;

    // 这里指定卡牌文本和大图使用的能量图标路径。
    // res://GuZhenRenPersonal/... 里的 GuZhenRen 是 PCK 资源目录，不是 C# namespace。
    public override string? BigEnergyIconPath => $"{Entry.ResPath}/images/characters/energy_big.png";
    public override string? TextEnergyIconPath => $"{Entry.ResPath}/images/characters/energy_text.png";

    public override Color DeckEntryCardColor => new(0.88f, 0.88f, 0.88f);
    public override Color EnergyOutlineColor => new(0.08f, 0.18f, 0.24f);


    // 卡框颜色由 HSV 材质控制（h=0.603, s=0.19, v=1.2）。
    private static readonly Material? _poolFrameMaterial =
        MaterialUtils.CreateHsvShaderMaterial(0.56f, 0.19f, 1.2f);
    public override Material? PoolFrameMaterial => _poolFrameMaterial;

    // false 表示这是角色专属卡池，不是事件/状态那类无色卡池。
    public override bool IsColorless => false;
}
