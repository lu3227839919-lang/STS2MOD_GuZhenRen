using Godot;

using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Characters;

/// <summary>
/// 杀招推演系统牌的隐藏卡池：不出现在任何奖励/掉落/卡牌库中，
/// 只由空窍三转后的战斗开始流程生成。
/// </summary>
public sealed class GuZhenRenShaZhaoDerivationCardPool
    : TypeListCardPoolModel
{
    public override string Title => "GuZhenRenShaZhaoDerivation";

    public override string EnergyColorName => "GuZhenRen";

    public override Color DeckEntryCardColor =>
        GuZhenRenCardVisualStyle.CardBackgroundColor;

    public override bool IsColorless => false;
}
