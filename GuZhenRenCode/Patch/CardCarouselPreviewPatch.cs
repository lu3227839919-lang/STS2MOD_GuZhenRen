using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

using Godot;

using GuZhenRen.Cards.Interfaces;

using HarmonyLib;

using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Saves;

namespace GuZhenRen.Patches;

/// <summary>
/// 将 ICarouselCard 移植到 STS2 的悬停卡牌预览系统。
///
/// 工作流程：
///
/// 1. CardModel.HoverTips 生成时，把当前轮播卡加入 CardHoverTip；
/// 2. NHoverTipSet 创建后记录该预览节点；
/// 3. NHoverTipSet._Process 中按单调时钟切换卡牌模型；
/// 4. 只更新已有 NCard，不反复销毁和重建整个悬停提示。
///
/// 该功能完全属于本地 UI，不推进游戏 RNG，也不影响多人游戏状态。
/// </summary>
internal static class CardCarouselPreviewPatch
{
    private const string HarmonyId =
        Entry.ModId + ".CardCarouselPreview";

    private const double DefaultIntervalSeconds =
        2.5d;

    private const ulong MinimumIntervalMilliseconds =
        100UL;

    private sealed class CarouselCardHoverTip
        : CardHoverTip
    {
        internal CarouselCardHoverTip(
            ICarouselCard source,
            CardModel card,
            ulong timeBucket
        )
            : base(card)
        {
            Source = source;
            TimeBucket = timeBucket;
        }

        internal ICarouselCard Source { get; }

        internal ulong TimeBucket { get; }
    }

    private sealed class ActiveCarouselState
    {
        internal ActiveCarouselState(
            ICarouselCard source,
            CardModel currentPreview,
            ulong timeBucket
        )
        {
            Source = source;
            CurrentPreview = currentPreview;
            TimeBucket = timeBucket;
        }

        internal ICarouselCard Source { get; }

        internal CardModel CurrentPreview { get; set; }

        internal ulong TimeBucket { get; set; }
    }

    private static ConditionalWeakTable<
        NHoverTipSet,
        ActiveCarouselState
    > _activeCarousels = new();

    private static bool _initialized;

    internal static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        MethodInfo? hoverTipsGetter =
            AccessTools.PropertyGetter(
                typeof(CardModel),
                nameof(CardModel.HoverTips)
            );
        MethodInfo? hoverTipSetInit =
            AccessTools.DeclaredMethod(
                typeof(NHoverTipSet),
                "Init",
                [
                    typeof(Control),
                    typeof(IEnumerable<IHoverTip>),
                ]
            );
        MethodInfo? hoverTipSetProcess =
            AccessTools.DeclaredMethod(
                typeof(NHoverTipSet),
                nameof(NHoverTipSet._Process),
                [typeof(double)]
            );

        if (hoverTipsGetter == null ||
            hoverTipSetInit == null ||
            hoverTipSetProcess == null)
        {
            throw new MissingMemberException(
                "卡牌轮播预览所需的 STS2 HoverTip 成员不存在。"
            );
        }

        Harmony harmony = new(HarmonyId);

        try
        {
            harmony.Patch(
                hoverTipsGetter,
                postfix: new HarmonyMethod(
                    typeof(CardCarouselPreviewPatch),
                    nameof(CardHoverTipsPostfix)
                )
            );

            harmony.Patch(
                hoverTipSetInit,
                postfix: new HarmonyMethod(
                    typeof(CardCarouselPreviewPatch),
                    nameof(HoverTipSetInitPostfix)
                )
            );

            harmony.Patch(
                hoverTipSetProcess,
                postfix: new HarmonyMethod(
                    typeof(CardCarouselPreviewPatch),
                    nameof(HoverTipSetProcessPostfix)
                )
            );

            _initialized = true;
        }
        catch
        {
            harmony.UnpatchAll(HarmonyId);
            _activeCarousels = new();
            throw;
        }
    }

    internal static void Uninitialize()
    {
        try
        {
            new Harmony(HarmonyId)
                .UnpatchAll(HarmonyId);
        }
        finally
        {
            _initialized = false;
            _activeCarousels = new();
        }
    }

    /// <summary>
    /// 给实现 ICarouselCard 的卡牌追加当前轮播项。
    ///
    /// 这里把结果物化为 List，确保 NHoverTipSet.Init 原方法与后缀
    /// 读取的是同一张预览实例。
    /// </summary>
    private static void CardHoverTipsPostfix(
        CardModel __instance,
        ref IEnumerable<IHoverTip> __result
    )
    {
        if (__instance is not
            ICarouselCard carousel)
        {
            return;
        }

        try
        {
            if (!TryGetCurrentPreview(
                    carousel,
                    out CardModel? preview,
                    out ulong timeBucket
                ))
            {
                return;
            }

            List<IHoverTip> tips =
                __result?.ToList() ?? [];

            tips.Add(
                new CarouselCardHoverTip(
                    carousel,
                    preview,
                    timeBucket
                )
            );

            __result = tips;
        }
        catch (Exception exception)
        {
            Entry.Logger.Info(
                "创建卡牌轮播预览失败：" +
                exception
            );
        }
    }

    /// <summary>
    /// 记录实际加入悬停提示的轮播卡实例。
    /// </summary>
    private static void HoverTipSetInitPostfix(
        NHoverTipSet __instance,
        IEnumerable<IHoverTip> hoverTips
    )
    {
        CarouselCardHoverTip? carouselTip =
            hoverTips
                .OfType<CarouselCardHoverTip>()
                .FirstOrDefault();

        if (carouselTip == null)
        {
            return;
        }

        _activeCarousels.Remove(__instance);
        _activeCarousels.Add(
            __instance,
            new ActiveCarouselState(
                carouselTip.Source,
                carouselTip.Card,
                carouselTip.TimeBucket
            )
        );
    }

    /// <summary>
    /// 悬停提示保持打开时，达到间隔后只替换轮播卡 NCard 的模型。
    /// </summary>
    private static void HoverTipSetProcessPostfix(
        NHoverTipSet __instance
    )
    {
        if (!_activeCarousels.TryGetValue(
                __instance,
                out ActiveCarouselState? state
            ))
        {
            return;
        }

        ulong currentBucket =
            GetCurrentTimeBucket(
                state.Source
            );

        if (currentBucket ==
            state.TimeBucket)
        {
            return;
        }

        // 即使刷新失败，也等待下一个时间段再重试，避免每帧刷日志。
        state.TimeBucket = currentBucket;

        try
        {
            if (!TryGetCurrentPreview(
                    state.Source,
                    out CardModel? nextPreview,
                    out _
                ))
            {
                return;
            }

            NCard? previewNode =
                FindPreviewNode(
                    __instance,
                    state.CurrentPreview
                );

            if (previewNode == null)
            {
                return;
            }

            previewNode.Model = nextPreview;
            previewNode.UpdateVisuals(
                PileType.Deck,
                CardPreviewMode.Normal
            );

            state.CurrentPreview =
                nextPreview;

            SaveManager.Instance?
                .MarkCardAsSeen(
                    nextPreview.CanonicalInstance
                );
        }
        catch (Exception exception)
        {
            Entry.Logger.Info(
                "更新卡牌轮播预览失败：" +
                exception
            );
        }
    }

    private static NCard? FindPreviewNode(
        NHoverTipSet hoverTipSet,
        CardModel currentPreview
    )
    {
        NHoverTipCardContainer? cardContainer =
            hoverTipSet
                .GetChildren()
                .OfType<NHoverTipCardContainer>()
                .FirstOrDefault();

        if (cardContainer == null)
        {
            return null;
        }

        foreach (Control holder in cardContainer
                     .GetChildren()
                     .OfType<Control>())
        {
            NCard? cardNode =
                holder.GetNodeOrNull<NCard>(
                    "%Card"
                );

            if (cardNode != null &&
                ReferenceEquals(
                    cardNode.Model,
                    currentPreview
                ))
            {
                return cardNode;
            }
        }

        return null;
    }

    private static bool TryGetCurrentPreview(
        ICarouselCard carousel,
        [NotNullWhen(true)] out CardModel? preview,
        out ulong timeBucket
    )
    {
        IReadOnlyList<CardModel>? candidates =
            carousel.GetCarouselCards();

        if (candidates == null ||
            candidates.Count == 0)
        {
            preview = null;
            timeBucket = 0UL;
            return false;
        }

        List<CardModel> visibleCards =
            candidates
                .Where(card =>
                    card != null &&
                    carousel
                        .ShouldShowCarouselCard(
                            card
                        )
                )
                .ToList();

        if (visibleCards.Count == 0)
        {
            preview = null;
            timeBucket = 0UL;
            return false;
        }

        timeBucket =
            GetCurrentTimeBucket(
                carousel
            );

        int index =
            (int)(
                timeBucket %
                (ulong)visibleCards.Count
            );

        CardModel selected =
            visibleCards[index];

        preview = selected.IsMutable
            ? selected
            : selected.ToMutable();

        return true;
    }

    private static ulong GetCurrentTimeBucket(
        ICarouselCard carousel
    )
    {
        double intervalSeconds =
            carousel.CarouselIntervalSeconds;

        if (!double.IsFinite(
                intervalSeconds
            ) ||
            intervalSeconds <= 0d)
        {
            intervalSeconds =
                DefaultIntervalSeconds;
        }

        ulong intervalMilliseconds =
            (ulong)Math.Max(
                MinimumIntervalMilliseconds,
                Math.Round(
                    intervalSeconds *
                    1000d
                )
            );

        return Time.GetTicksMsec() /
            intervalMilliseconds;
    }
}
