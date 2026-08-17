using Godot;

using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Characters;

/// <summary>
/// 旧版杀招推演系统牌的隐藏卡池兼容类型。
/// 当前杀招推演牌已经通过 AbstractGuZhenRenGeneratedCard 注册到
/// GuZhenRenGuCardPool；保留此类型是为了兼容旧存档/旧资源索引，
/// 不再向其中注册新卡。
/// </summary>
public sealed class GuZhenRenShaZhaoDerivationCardPool
    : TypeListCardPoolModel
{
    public override string Title => Entry.ModId + "ShaZhaoDerivation";

    public override string EnergyColorName => Entry.ModId;

    public override Color DeckEntryCardColor => new(0.88f, 0.88f, 0.88f);

    public override bool IsColorless => false;
}
