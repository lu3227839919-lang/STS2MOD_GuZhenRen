using GuZhenRen.Cards.Interfaces;
using GuZhenRen.Combat;

using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRen.Cards;

/// <summary>
/// 真正蛊虫牌的公共父类。
///
/// 杀招与虚影只继承 AbstractGuZhenRenCard，以复用品阶和流派数据；
/// 具体蛊虫应继承本类，从而显式获得 IGuWormCard 身份和蛊牌堆规则。
/// </summary>
public abstract class AbstractGuWormCard
    : AbstractGuZhenRenCard,
      IGuWormCard,
      ICarouselCard
{
    public override bool CanBeGeneratedInCombat => false;

    /// <summary>
    /// 当前转数没有衍生牌时不显示卡牌引用；具体蛊虫按真实生成
    /// 逻辑重写本方法，只返回当前转数能够生成的牌。
    /// </summary>
    public virtual IReadOnlyList<CardModel> GetCarouselCards() => [];

    /// <summary>
    /// 普通蛊虫默认消耗 1 点元气；合练蛊在公共父类中改为 2。
    /// </summary>
    public virtual int YuanQiCost => 1;

    /// <summary>
    /// 默认在耗尽后的第2个回合开始恢复；具体蛊虫可以按转数重写。
    /// </summary>
    public virtual int RecoveryDelayTurns => 2;

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
