using MegaCrit.Sts2.Core.Entities.Cards;

using STS2RitsuLib.Content;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Keywords;

namespace GuZhenRen.Cards;

/// <summary>
/// 蛊真人模组自定义卡牌关键词。
///
/// 所有关键词的 CardDescriptionPlacement 都是 None：
/// 关键词不会自动插入卡面正文，但会在卡牌附近生成悬浮提示框。
/// </summary>
[RegisterOwnedCardKeyword(nameof(XuYing), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(Unique), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(XianGu), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(GuChong), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(CuiDong), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(HuiFu), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(HeLian), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(YuanQi), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(XianYuan), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(GuangHui), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(ZheGuang), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(ZhaoPo), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(JuGuang), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(DingGuang), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(YaoHua), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(LiuGuang), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(PoJing), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(XueYuan), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(XueJi), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(XueQi), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(YueXiang), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(CanYue), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(YingYue), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(ManYue), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(XueTai), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(TaiDong), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(PoTai), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(FuHua), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(TunJi), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(ZongEDu), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(YiHai), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(XueLu), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(LiuXue), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(XueYin), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(YiChu), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(ZhuiJi), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(XueHe), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(Rank1), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(Rank2), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(Rank3), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(Rank4), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(Rank5), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(Rank6), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(Rank7), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(Rank8), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(Rank9), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
public sealed class GuZhenRenKeywords
{
    public static readonly CardKeyword XuYing = Create(nameof(XuYing));
    public static readonly CardKeyword Unique = Create(nameof(Unique));
    public static readonly CardKeyword XianGu = Create(nameof(XianGu));
    public static readonly CardKeyword GuChong = Create(nameof(GuChong));
    public static readonly CardKeyword CuiDong = Create(nameof(CuiDong));
    public static readonly CardKeyword HuiFu = Create(nameof(HuiFu));
    public static readonly CardKeyword HeLian = Create(nameof(HeLian));
    public static readonly CardKeyword YuanQi = Create(nameof(YuanQi));
    public static readonly CardKeyword XianYuan = Create(nameof(XianYuan));
    public static readonly CardKeyword GuangHui = Create(nameof(GuangHui));
    public static readonly CardKeyword ZheGuang = Create(nameof(ZheGuang));
    public static readonly CardKeyword ZhaoPo = Create(nameof(ZhaoPo));
    public static readonly CardKeyword JuGuang = Create(nameof(JuGuang));
    public static readonly CardKeyword DingGuang = Create(nameof(DingGuang));
    public static readonly CardKeyword YaoHua = Create(nameof(YaoHua));
    public static readonly CardKeyword LiuGuang = Create(nameof(LiuGuang));
    public static readonly CardKeyword PoJing = Create(nameof(PoJing));
    public static readonly CardKeyword XueYuan = Create(nameof(XueYuan));
    public static readonly CardKeyword XueJi = Create(nameof(XueJi));
    public static readonly CardKeyword XueQi = Create(nameof(XueQi));
    public static readonly CardKeyword YueXiang = Create(nameof(YueXiang));
    public static readonly CardKeyword CanYue = Create(nameof(CanYue));
    public static readonly CardKeyword YingYue = Create(nameof(YingYue));
    public static readonly CardKeyword ManYue = Create(nameof(ManYue));
    public static readonly CardKeyword XueTai = Create(nameof(XueTai));
    public static readonly CardKeyword TaiDong = Create(nameof(TaiDong));
    public static readonly CardKeyword PoTai = Create(nameof(PoTai));
    public static readonly CardKeyword FuHua = Create(nameof(FuHua));
    public static readonly CardKeyword TunJi = Create(nameof(TunJi));
    public static readonly CardKeyword ZongEDu = Create(nameof(ZongEDu));
    public static readonly CardKeyword YiHai = Create(nameof(YiHai));
    public static readonly CardKeyword XueLu = Create(nameof(XueLu));
    public static readonly CardKeyword LiuXue = Create(nameof(LiuXue));
    public static readonly CardKeyword XueYin = Create(nameof(XueYin));
    public static readonly CardKeyword YiChu = Create(nameof(YiChu));
    public static readonly CardKeyword ZhuiJi = Create(nameof(ZhuiJi));
    public static readonly CardKeyword XueHe = Create(nameof(XueHe));
    public static readonly CardKeyword Rank1 = Create(nameof(Rank1));
    public static readonly CardKeyword Rank2 = Create(nameof(Rank2));
    public static readonly CardKeyword Rank3 = Create(nameof(Rank3));
    public static readonly CardKeyword Rank4 = Create(nameof(Rank4));
    public static readonly CardKeyword Rank5 = Create(nameof(Rank5));
    public static readonly CardKeyword Rank6 = Create(nameof(Rank6));
    public static readonly CardKeyword Rank7 = Create(nameof(Rank7));
    public static readonly CardKeyword Rank8 = Create(nameof(Rank8));
    public static readonly CardKeyword Rank9 = Create(nameof(Rank9));

    public static IReadOnlySet<CardKeyword> RankKeywords { get; } =
        new HashSet<CardKeyword>
        {
            Rank1, Rank2, Rank3, Rank4, Rank5,
            Rank6, Rank7, Rank8, Rank9,
        };

    public static IReadOnlySet<CardKeyword> ParasiteKeywords { get; } =
        new HashSet<CardKeyword>
        {
            XueJi, XueQi, YueXiang, CanYue, YingYue, ManYue,
            XueTai, TaiDong, PoTai, FuHua, TunJi,
        };

    /// <summary>
    /// 本模组注册的全部自定义关键词。
    /// UI 可见性补丁只过滤这个集合，不影响游戏本体的消耗、保留等提示。
    /// </summary>
    public static IReadOnlySet<CardKeyword> OwnedKeywords { get; } =
        new HashSet<CardKeyword>
        {
            XuYing, Unique, XianGu, GuChong, CuiDong, HuiFu,
            HeLian, YuanQi, XianYuan, GuangHui, ZheGuang,
            ZhaoPo, JuGuang, DingGuang, YaoHua, LiuGuang,
            PoJing, XueYuan, XueJi, XueQi, YueXiang, CanYue,
            YingYue, ManYue, XueTai, TaiDong, PoTai, FuHua,
            TunJi, ZongEDu, YiHai, XueLu, LiuXue, XueYin,
            YiChu, ZhuiJi, XueHe, Rank1, Rank2, Rank3, Rank4,
            Rank5, Rank6, Rank7, Rank8, Rank9,
        };

    public static CardKeyword GetRankKeyword(int rank) => rank switch
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

    private static CardKeyword Create(string localName) =>
        ModContentRegistry
            .GetQualifiedKeywordId(Entry.ModId, localName)
            .GetModCardKeyword();

    private GuZhenRenKeywords()
    {
    }
}
