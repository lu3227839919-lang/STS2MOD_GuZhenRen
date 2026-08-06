using GuZhenRen.Cards.Interfaces;
using GuZhenRen.Combat;

using MegaCrit.Sts2.Core.Entities.Cards;
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
    /// 所有蛊虫必定显示：
    /// 蛊虫、当前转数、催动、恢复、元气。
    ///
    /// 六转以上额外显示仙蛊与仙元。各蛊虫自己的机制词由类型映射
    /// 追加；Distinct 保证派生类再次追加同一关键词时不会重复显示。
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        base.CanonicalKeywords
            .Concat(GetCommonGuKeywords())
            .Concat(GetMechanicKeywords())
            .Distinct();

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

    private IEnumerable<CardKeyword> GetCommonGuKeywords()
    {
        yield return GuZhenRenKeywords.GuChong;
        yield return GuZhenRenKeywords.GetRankKeyword(GuRank);
        yield return GuZhenRenKeywords.CuiDong;
        yield return GuZhenRenKeywords.HuiFu;
        yield return GuZhenRenKeywords.YuanQi;

        if (GuRank >= 6)
        {
            yield return GuZhenRenKeywords.XianGu;
            yield return GuZhenRenKeywords.XianYuan;
        }
    }

    /// <summary>
    /// 为每种现有蛊虫附加真正相关的机制说明。
    ///
    /// 使用类型名映射可让所有现有蛊虫立即覆盖关键词提示，同时避免
    /// 把不相关的流派术语全部堆到每张卡上。新增蛊虫时只需在此增加
    /// 一项，或由具体卡牌继续重写 CanonicalKeywords。
    /// </summary>
    private IEnumerable<CardKeyword> GetMechanicKeywords()
    {
        return GetType().Name switch
        {
            "XiaoGuangGu" =>
            [
                GuZhenRenKeywords.GuangHui,
                GuZhenRenKeywords.JuGuang,
            ],
            "YueGuangGu" =>
            [
                GuZhenRenKeywords.GuangHui,
                GuZhenRenKeywords.YaoHua,
                GuZhenRenKeywords.ZhaoPo,
            ],
            "JingGuangGu" =>
            [
                GuZhenRenKeywords.GuangHui,
                GuZhenRenKeywords.ZheGuang,
                GuZhenRenKeywords.JuGuang,
            ],
            "DingGuangGu" =>
            [
                GuZhenRenKeywords.ZhaoPo,
                GuZhenRenKeywords.DingGuang,
            ],
            "LiuGuangGu" =>
            [
                GuZhenRenKeywords.ZheGuang,
                GuZhenRenKeywords.ZhaoPo,
            ],
            "YueMangGu" =>
            [
                GuZhenRenKeywords.GuangHui,
                GuZhenRenKeywords.YaoHua,
                GuZhenRenKeywords.ZhaoPo,
            ],
            "JingHuiGu" =>
            [
                GuZhenRenKeywords.GuangHui,
                GuZhenRenKeywords.ZhaoPo,
                GuZhenRenKeywords.JuGuang,
                GuZhenRenKeywords.LiuGuang,
                GuZhenRenKeywords.PoJing,
            ],
            "YuPiGu" =>
            [
                GuZhenRenKeywords.GuangHui,
                GuZhenRenKeywords.ZheGuang,
            ],
            "XueQiGu" =>
            [
                GuZhenRenKeywords.XueQi,
                GuZhenRenKeywords.FuHua,
                GuZhenRenKeywords.YiHai,
            ],
            "XueYueGu" =>
            [
                GuZhenRenKeywords.YueXiang,
                GuZhenRenKeywords.ZongEDu,
            ],
            "XueTaiGu" =>
            [
                GuZhenRenKeywords.XueTai,
                GuZhenRenKeywords.TaiDong,
                GuZhenRenKeywords.TunJi,
                GuZhenRenKeywords.FuHua,
            ],
            "XueChiGu" =>
            [
                GuZhenRenKeywords.XueYuan,
                GuZhenRenKeywords.YiChu,
            ],
            "XuePiGu" =>
            [
                GuZhenRenKeywords.XueYuan,
                GuZhenRenKeywords.XueLu,
            ],
            "XueLuGu" =>
            [
                GuZhenRenKeywords.YiHai,
                GuZhenRenKeywords.XueLu,
            ],
            "DaoChiXueFuGu" =>
            [
                GuZhenRenKeywords.YiHai,
                GuZhenRenKeywords.ZhuiJi,
            ],
            "XueFuWangGu" =>
            [
                GuZhenRenKeywords.YiHai,
                GuZhenRenKeywords.XueLu,
                GuZhenRenKeywords.ZhuiJi,
            ],
            "XueHeJiaGu" =>
            [
                GuZhenRenKeywords.XueYuan,
                GuZhenRenKeywords.XueLu,
                GuZhenRenKeywords.XueHe,
            ],
            _ => [],
        };
    }
}
