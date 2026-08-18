using GuZhenRen.Characters;
using GuZhenRen.Cards.Interfaces;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.ImmortalEssence;

/// <summary>
/// 四张仙元牌的公共父类。
/// 仙元牌均为 0 费、保留、不能手动打出，并且只由仙窍在战斗中生成。
/// 它们留在手牌中，由仙蛊催动流程按剩余单位自动消耗。
/// </summary>
[RegisterCard(
    typeof(GuZhenRenXianYuanCardPool),
    Inherit = true
)]
public abstract class AbstractXianYuanCard
    : ModCardTemplate,
      ICarouselCard
{
    /// <summary>
    /// 仙元牌固定属于仙元隐藏卡池。
    /// </summary>
    public override CardPoolModel Pool =>
        ModelDb.CardPool<GuZhenRenXianYuanCardPool>();

    /// <summary>
    /// 以“一次六转仙蛊催动”为单位的总价值。
    /// </summary>
    public abstract int ActivationUnits { get; }

    /// <summary>
    /// 描述中作为价值参照的上一档仙元牌。
    /// </summary>
    protected virtual CardModel? ReferencedCard => null;

    /// <summary>
    /// 每张具体仙元牌必须声明非空的静态图片路径。
    /// 这样 RitsuLib 分析器和运行时才能在资源缺失时给出诊断。
    /// </summary>
    public override CardAssetProfile AssetProfile =>
        global::GuZhenRen.Cards.CardImageCatalog.Create(GetType());

    protected AbstractXianYuanCard()
        : base(
            baseCost: 0,
            type: CardType.Skill,
            rarity: CardRarity.Rare,
            target: TargetType.Self,
            showInCardLibrary: true
        )
    {
    }

    public override bool CanBeGeneratedInCombat => false;

    protected sealed override bool IsPlayable => false;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        base.CanonicalKeywords
            .Append(CardKeyword.Retain)
            .Append(CardKeyword.Exhaust);

    protected override void AddExtraArgsToDescription(
        LocString description
    )
    {
        base.AddExtraArgsToDescription(description);
        description.Add("ActivationUnits", ActivationUnits);
        description.Add(
            "RemainingActivationUnits",
            ImmortalEssenceSystem.GetRemainingUnits(this)
        );
    }

    public IReadOnlyList<CardModel> GetCarouselCards()
    {
        return ReferencedCard is { } card
            ? [card]
            : [];
    }

    protected override void OnUpgrade()
    {
        // Java 原版升级不改变实际效果。
    }
}
