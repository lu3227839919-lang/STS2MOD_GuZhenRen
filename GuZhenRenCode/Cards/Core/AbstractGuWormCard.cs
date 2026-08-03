using GuZhenRen.Combat;

using MegaCrit.Sts2.Core.Entities.Cards;

namespace GuZhenRen.Cards;

/// <summary>
/// 真正蛊虫牌的公共父类。
///
/// 杀招与虚影只继承 AbstractGuZhenRenCard，以复用品阶和流派数据；
/// 具体蛊虫应继承本类，从而显式获得 IGuWormCard 身份和蛊牌堆规则。
/// </summary>
public abstract class AbstractGuWormCard
    : AbstractGuZhenRenCard,
      IGuWormCard
{
    public override bool CanBeGeneratedInCombat => false;

    /// <summary>
    /// 蛊虫左上角展示元气图标，而不是角色的原生能量图标。
    /// </summary>
    public override string? CustomEnergyIconPath =>
        YuanQiSystem.LargeIconPath;

    protected AbstractGuWormCard(
        int baseCost,
        CardType type,
        CardRarity rarity,
        TargetType target,
        bool showInCardLibrary = true
    )
        : base(
            baseCost,
            type,
            rarity,
            target,
            showInCardLibrary
        )
    {
    }
}
