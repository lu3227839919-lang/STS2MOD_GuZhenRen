using Godot;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Characters;

public sealed class GuZhenRenCardPool : TypeListCardPoolModel
{
    /// <summary>
    /// Auxiliary Gu Zhen Ren card pool for non-Gu starter and generated cards.
    /// The character's primary card pool is GuZhenRenGuCardPool; phantom cards
    /// and killer-move cards are registered in their dedicated pools instead.
    /// </summary>
    // Title 和 EnergyColorName 是池子的稳定标识，不是玩家看到的角色名。
    // 自定义角色卡、遗物、药水池保持同一个 EnergyColorName，方便实验室和文本统一读取能量图标。
    public override string Title => "GuZhenRen";
    public override string EnergyColorName => "GuZhenRen";

    // 这里指定卡牌文本和大图使用的能量图标路径。
    // res://GuZhenRen/... 里的 GuZhenRen 是 PCK 资源目录，不是 C# namespace。
    public override string? BigEnergyIconPath => $"{Entry.ResPath}/images/characters/energy_big.png";
    public override string? TextEnergyIconPath => $"{Entry.ResPath}/images/characters/energy_text.png";

    public override Color DeckEntryCardColor =>
        GuZhenRenCardVisualStyle.CardBackgroundColor;
    public override Color EnergyOutlineColor => new(0.08f, 0.18f, 0.24f);


    // 所有蛊真人卡统一使用黑色卡框与灰色卡面。
    public override Material? PoolFrameMaterial =>
        GuZhenRenCardVisualStyle.FrameMaterial;

    // false 表示这是角色专属卡池，不是事件/状态那类无色卡池。
    public override bool IsColorless => false;
}
