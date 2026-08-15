using MegaCrit.Sts2.Core.Models;

namespace GuZhenRen.Cards.Interfaces;

/// <summary>
/// 为一张卡牌提供可轮播显示的关联卡牌预览。
///
/// STS1 Java 版本通过 AbstractCard.cardsToPreview 显示单张关联卡；
/// STS2 没有该字段，因此由 CardCarouselPreviewPatch 把当前关联卡
/// 作为 CardHoverTip 加入悬停提示，并在提示保持打开时定时切换。
/// </summary>
public interface ICarouselCard
{
    /// <summary>
    /// 返回参与轮播的关联卡牌模型。
    ///
    /// 可以返回规范模型或可变模型；补丁会在显示前确保预览模型可用。
    /// 返回顺序就是轮播顺序。
    /// </summary>
    IReadOnlyList<CardModel> GetCarouselCards();

    /// <summary>
    /// 每张关联卡显示的时间，单位为秒。
    /// </summary>
    double CarouselIntervalSeconds => 2.5d;

    /// <summary>
    /// 动态过滤某张关联卡是否应出现在当前轮播中。
    /// </summary>
    bool ShouldShowCarouselCard(
        CardModel card
    ) => true;
}
