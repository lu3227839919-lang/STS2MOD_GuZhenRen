using MegaCrit.Sts2.Core.Entities.Cards;

using STS2RitsuLib.Content;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Keywords;

namespace GuZhenRen.Cards;

/// <summary>
/// 蛊真人模组自定义卡牌关键词。
///
/// “转数”和“仙蛊”是由卡牌保存的 GuRank 动态派生出的展示关键词；
/// “唯一”只表示普通的玩家牌组内唯一规则，不再承担仙蛊标识或仙蛊唯一性。
///
/// 每个关键词通过 RegisterOwnedCardKeyword 注册，
/// 再通过 ModContentRegistry 取得运行时 CardKeyword。
///
/// RitsuLib 的关键词注册容器不能声明为 static class。
/// </summary>
[RegisterOwnedCardKeyword(
    nameof(XuYing),
    CardDescriptionPlacement =
        ModKeywordCardDescriptionPlacement.BeforeCardDescription
)]
[RegisterOwnedCardKeyword(
    nameof(Unique),
    CardDescriptionPlacement =
        ModKeywordCardDescriptionPlacement.BeforeCardDescription
)]
[RegisterOwnedCardKeyword(
    nameof(XianGu),
    CardDescriptionPlacement =
        ModKeywordCardDescriptionPlacement.BeforeCardDescription
)]
[RegisterOwnedCardKeyword(
    nameof(Rank1),
    CardDescriptionPlacement =
        ModKeywordCardDescriptionPlacement.BeforeCardDescription,
    IncludeInCardHoverTip = false
)]
[RegisterOwnedCardKeyword(
    nameof(Rank2),
    CardDescriptionPlacement =
        ModKeywordCardDescriptionPlacement.BeforeCardDescription,
    IncludeInCardHoverTip = false
)]
[RegisterOwnedCardKeyword(
    nameof(Rank3),
    CardDescriptionPlacement =
        ModKeywordCardDescriptionPlacement.BeforeCardDescription,
    IncludeInCardHoverTip = false
)]
[RegisterOwnedCardKeyword(
    nameof(Rank4),
    CardDescriptionPlacement =
        ModKeywordCardDescriptionPlacement.BeforeCardDescription,
    IncludeInCardHoverTip = false
)]
[RegisterOwnedCardKeyword(
    nameof(Rank5),
    CardDescriptionPlacement =
        ModKeywordCardDescriptionPlacement.BeforeCardDescription,
    IncludeInCardHoverTip = false
)]
[RegisterOwnedCardKeyword(
    nameof(Rank6),
    CardDescriptionPlacement =
        ModKeywordCardDescriptionPlacement.BeforeCardDescription,
    IncludeInCardHoverTip = false
)]
[RegisterOwnedCardKeyword(
    nameof(Rank7),
    CardDescriptionPlacement =
        ModKeywordCardDescriptionPlacement.BeforeCardDescription,
    IncludeInCardHoverTip = false
)]
[RegisterOwnedCardKeyword(
    nameof(Rank8),
    CardDescriptionPlacement =
        ModKeywordCardDescriptionPlacement.BeforeCardDescription,
    IncludeInCardHoverTip = false
)]
[RegisterOwnedCardKeyword(
    nameof(Rank9),
    CardDescriptionPlacement =
        ModKeywordCardDescriptionPlacement.BeforeCardDescription,
    IncludeInCardHoverTip = false
)]
public sealed class GuZhenRenKeywords
{
    /// <summary>
    /// “虚影”卡牌属性。
    /// </summary>
    public static readonly CardKeyword XuYing =
        Create(nameof(XuYing));

    /// <summary>
    /// 普通“唯一”卡牌属性。
    ///
    /// 只表示同一玩家的永久牌组中只能存在一张同名卡牌。
    /// 仙蛊不需要、也不会自动获得此关键词。
    /// </summary>
    public static readonly CardKeyword Unique =
        Create(nameof(Unique));

    /// <summary>
    /// “仙蛊”展示关键词。
    ///
    /// 仙蛊的整局跨玩家唯一规则由 GuZhenRenCardRules.IsXianGu
    /// 独立判定，不依赖此展示关键词。
    /// </summary>
    public static readonly CardKeyword XianGu =
        Create(nameof(XianGu));

    public static readonly CardKeyword Rank1 =
        Create(nameof(Rank1));

    public static readonly CardKeyword Rank2 =
        Create(nameof(Rank2));

    public static readonly CardKeyword Rank3 =
        Create(nameof(Rank3));

    public static readonly CardKeyword Rank4 =
        Create(nameof(Rank4));

    public static readonly CardKeyword Rank5 =
        Create(nameof(Rank5));

    public static readonly CardKeyword Rank6 =
        Create(nameof(Rank6));

    public static readonly CardKeyword Rank7 =
        Create(nameof(Rank7));

    public static readonly CardKeyword Rank8 =
        Create(nameof(Rank8));

    public static readonly CardKeyword Rank9 =
        Create(nameof(Rank9));

    /// <summary>
    /// 全部动态转数关键词。
    ///
    /// 每次计算卡牌本地关键词时先移除旧转数，再按当前保存的 GuRank
    /// 添加一个正确转数，避免复制、读档或升转后保留旧标签。
    /// </summary>
    public static IReadOnlySet<CardKeyword>
        RankKeywords { get; } =
            new HashSet<CardKeyword>
            {
                Rank1,
                Rank2,
                Rank3,
                Rank4,
                Rank5,
                Rank6,
                Rank7,
                Rank8,
                Rank9,
            };

    /// <summary>
    /// 获取指定转数对应的展示关键词。
    /// </summary>
    public static CardKeyword GetRankKeyword(
        int rank
    )
    {
        return rank switch
        {
            1 => Rank1,
            2 => Rank2,
            3 => Rank3,
            4 => Rank4,
            5 => Rank5,
            6 => Rank6,
            7 => Rank7,
            8 => Rank8,
            9 => Rank9,
            _ => throw new ArgumentOutOfRangeException(
                nameof(rank),
                rank,
                "蛊卡展示转数必须位于一至九转。"
            ),
        };
    }

    /// <summary>
    /// 获取当前 Mod 拥有的运行时卡牌关键词。
    /// </summary>
    private static CardKeyword Create(
        string localName
    )
    {
        return ModContentRegistry
            .GetQualifiedKeywordId(
                Entry.ModId,
                localName
            )
            .GetModCardKeyword();
    }

    private GuZhenRenKeywords()
    {
    }
}
