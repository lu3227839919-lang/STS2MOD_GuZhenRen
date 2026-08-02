using Godot;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Characters;

public sealed class GuZhenRenShaZhaoCardPool : TypeListCardPoolModel
{
    // Title 和 EnergyColorName 是池子的稳定标识，不是玩家看到的角色名。
    // 自定义角色卡、遗物、药水池保持同一个 EnergyColorName，方便实验室和文本统一读取能量图标。
    public override string Title => "GuZhenRenShaZhao";
    public override string EnergyColorName => "GuZhenRen";

    // 这里指定卡牌文本和大图使用的能量图标路径。
    // res://GuZhenRen/... 里的 GuZhenRen 是 PCK 资源目录，不是 C# namespace。
    public override string? BigEnergyIconPath => $"{Entry.ResPath}/images/characters/energy_big.png";
    public override string? TextEnergyIconPath => $"{Entry.ResPath}/images/characters/energy_text.png";

    public override Color DeckEntryCardColor => GuZhenRenCharacter.ThemeColor;
    public override Color EnergyOutlineColor => new(0.08f, 0.18f, 0.24f);


    // 普通卡统一使用水墨卡框材质；单张卡自己的 FrameMaterialPath 仍可覆盖它。
    public override Material? PoolFrameMaterial =>
    GD.Load<Material>(
        $"{Entry.ResPath}/materials/card_frame_ink_wash.tres"
    );

    // false 表示这是角色专属卡池，不是事件/状态那类无色卡池。
    public override bool IsColorless => false;
}
