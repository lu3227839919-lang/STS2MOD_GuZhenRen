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
[RegisterOwnedCardKeyword(nameof(CuiDong), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(HuiFu), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(HeLian), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(XianYuan), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(ZheGuang), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(ZhaoPo), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(JuGuang), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(DingGuang), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(YaoHua1), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(YaoHua2), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(YaoHua3), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(YaoHua4), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(YaoHua5), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(YaoHua6), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(YaoHua7), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(YaoHua8), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(YaoHua9), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
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
public sealed class GuZhenRenKeywords
{
    public static readonly CardKeyword XuYing = Create(nameof(XuYing));
    public static readonly CardKeyword Unique = Create(nameof(Unique));
    public static readonly CardKeyword XianGu = Create(nameof(XianGu));
    public static readonly CardKeyword CuiDong = Create(nameof(CuiDong));
    public static readonly CardKeyword HuiFu = Create(nameof(HuiFu));
    public static readonly CardKeyword HeLian = Create(nameof(HeLian));
    public static readonly CardKeyword XianYuan = Create(nameof(XianYuan));
    public static readonly CardKeyword ZheGuang = Create(nameof(ZheGuang));
    public static readonly CardKeyword ZhaoPo = Create(nameof(ZhaoPo));
    public static readonly CardKeyword JuGuang = Create(nameof(JuGuang));
    public static readonly CardKeyword DingGuang = Create(nameof(DingGuang));
    public static readonly CardKeyword YaoHua1 = Create(nameof(YaoHua1));
    public static readonly CardKeyword YaoHua2 = Create(nameof(YaoHua2));
    public static readonly CardKeyword YaoHua3 = Create(nameof(YaoHua3));
    public static readonly CardKeyword YaoHua4 = Create(nameof(YaoHua4));
    public static readonly CardKeyword YaoHua5 = Create(nameof(YaoHua5));
    public static readonly CardKeyword YaoHua6 = Create(nameof(YaoHua6));
    public static readonly CardKeyword YaoHua7 = Create(nameof(YaoHua7));
    public static readonly CardKeyword YaoHua8 = Create(nameof(YaoHua8));
    public static readonly CardKeyword YaoHua9 = Create(nameof(YaoHua9));
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
    /// <summary>
    /// 旧版本可能已经把这些展示关键词写进卡牌实例或多人快照。
    /// 保留对应 ID 仅用于清理；它们不再注册，也不会重新加入卡牌。
    /// </summary>
    public static IReadOnlySet<CardKeyword> RemovedDisplayKeywords { get; } =
        new HashSet<CardKeyword>
        {
            Create("GuChong"),
            Create("YuanQi"),
            Create("GuangHui"),
            Create("YaoHua"),
            Create("Rank1"),
            Create("Rank2"),
            Create("Rank3"),
            Create("Rank4"),
            Create("Rank5"),
            Create("Rank6"),
            Create("Rank7"),
            Create("Rank8"),
            Create("Rank9"),
        };

    public static IReadOnlySet<CardKeyword> YaoHuaKeywords { get; } =
        new HashSet<CardKeyword>
        {
            YaoHua1, YaoHua2, YaoHua3,
            YaoHua4, YaoHua5, YaoHua6,
            YaoHua7, YaoHua8, YaoHua9,
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
            XuYing, Unique, XianGu, CuiDong, HuiFu,
            HeLian, XianYuan, ZheGuang,
            ZhaoPo, JuGuang, DingGuang,
            YaoHua1, YaoHua2, YaoHua3,
            YaoHua4, YaoHua5, YaoHua6,
            YaoHua7, YaoHua8, YaoHua9, LiuGuang,
            PoJing, XueYuan, XueJi, XueQi, YueXiang, CanYue,
            YingYue, ManYue, XueTai, TaiDong, PoTai, FuHua,
            TunJi, ZongEDu, YiHai, XueLu, LiuXue, XueYin,
            YiChu, ZhuiJi, XueHe,
        };

    public static CardKeyword GetYaoHuaKeyword(int threshold) =>
        threshold switch
    {
        1 => YaoHua1,
        2 => YaoHua2,
        3 => YaoHua3,
        4 => YaoHua4,
        5 => YaoHua5,
        6 => YaoHua6,
        7 => YaoHua7,
        8 => YaoHua8,
        9 => YaoHua9,
        _ => throw new ArgumentOutOfRangeException(
            nameof(threshold),
            threshold,
            "耀化阈值必须位于一至九点。"
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
