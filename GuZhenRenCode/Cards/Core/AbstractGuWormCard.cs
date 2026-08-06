using GuZhenRen.Aperture;
using GuZhenRen.Cards.HeLian;
using GuZhenRen.Cards.Interfaces;
using GuZhenRen.Combat;

using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRen.Cards;

/// <summary>
/// 真正蛊虫牌的公共父类。
///
/// 所有蛊虫在这里统一获得专有名词悬浮提示。关键词正文插入位置为
/// None，因此不会拉长卡面描述。
/// </summary>
public abstract class AbstractGuWormCard
    : AbstractGuZhenRenCard,
      IGuWormCard,
      ICarouselCard
{
    public override bool CanBeGeneratedInCombat => false;

    /// <summary>
    /// 所有蛊虫只显示会影响操作的催动与恢复说明。
    ///
    /// 六转以上额外显示仙蛊。各蛊虫自己的机制词由类型映射
    /// 追加；Distinct 保证派生类再次追加同一关键词时不会重复显示。
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        base.CanonicalKeywords
            .Concat(GetDisplayKeywordsFor(this))
            .Distinct();

    /// <summary>
    /// 按当前转数返回蛊牌应显示的全部本模组关键词。
    ///
    /// 与 CanonicalKeywords 使用同一套规则，但独立于实例的
    /// _keywords 快照：卡牌转数变化（升转/合练/读档/克隆）后快照
    /// 不会自动同步，UI 重建关键词时必须用本方法保证只显示
    /// 当前转数拥有的内容（低转不残留仙蛊、耀化等高转词）。
    /// </summary>
    internal static IEnumerable<CardKeyword> GetDisplayKeywordsFor(
        AbstractGuWormCard guWorm
    )
    {
        yield return GuZhenRenKeywords.CuiDong;
        yield return GuZhenRenKeywords.HuiFu;

        if (guWorm.GuRank >= GuZhenRenCardRules.XianGuRank)
        {
            yield return GuZhenRenKeywords.XianGu;
        }

        foreach (CardKeyword keyword in GetMechanicKeywords(guWorm))
        {
            yield return keyword;
        }

        if (guWorm is AbstractHeLianGuCard)
        {
            yield return GuZhenRenKeywords.HeLian;
        }
    }

    /// <summary>
    /// 当前转数没有衍生牌时不显示卡牌引用；具体蛊虫按真实生成
    /// 逻辑重写本方法，只返回当前转数能够生成的牌。
    /// </summary>
    public virtual IReadOnlyList<CardModel>
        GetCarouselCards() => [];

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

    /// <summary>
    /// 所有真正蛊虫都向卡面提供恢复回合数。派生类可继续覆盖同名
    /// 参数；LocString 会以最后一次写入为准，不会产生重复变量。
    /// </summary>
    protected override void AddExtraArgsToDescription(
        LocString description
    )
    {
        base.AddExtraArgsToDescription(description);
        description.Add("RecoveryTurns", RecoveryDelayTurns);
    }

    private static IEnumerable<CardKeyword> GetMechanicKeywords(
        IGuWormCard guWorm
    )
    {
        return guWorm.GetType().Name switch
        {
            "YueGuangGu" =>
            [
                GuZhenRenKeywords.GetYaoHuaKeyword(2),
            ],
            "YueMangGu" when guWorm.GuRank >= 5 =>
            [
                GuZhenRenKeywords.GetYaoHuaKeyword(2),
            ],
            "XueQiGu" =>
            [
                GuZhenRenKeywords.XueQi,
                GuZhenRenKeywords.FuHua,
                GuZhenRenKeywords.KeXue,
                GuZhenRenKeywords.ZiShi,
            ],
            "XueYueGu" =>
            [
                GuZhenRenKeywords.YueXiang,
                GuZhenRenKeywords.ZongEDu,
                GuZhenRenKeywords.KeXue,
            ],
            "XueTaiGu" =>
            [
                GuZhenRenKeywords.XueTai,
                GuZhenRenKeywords.TaiDong,
                GuZhenRenKeywords.TunJi,
                GuZhenRenKeywords.FuHua,
                GuZhenRenKeywords.KeXue,
                GuZhenRenKeywords.ZiShi,
            ],
            "XueChiGu" =>
            [
                GuZhenRenKeywords.YiChu,
            ],
            "XuePiGu" =>
            [
                GuZhenRenKeywords.KeXue,
            ],
            "DaoChiXueFuGu" =>
            [
                GuZhenRenKeywords.ZhuiJi,
                GuZhenRenKeywords.GetShiHaiKeyword(2),
            ],
            "XueFuWangGu" =>
            [
                GuZhenRenKeywords.ZhuiJi,
                GuZhenRenKeywords.GetShiHaiKeyword(2),
            ],
            "XueHeJiaGu" =>
            [
                GuZhenRenKeywords.KeXue,
            ],
            _ => [],
        };
    }
}
