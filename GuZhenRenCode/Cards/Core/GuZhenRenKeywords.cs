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
[RegisterOwnedCardKeyword(nameof(Unique), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(XianGu), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(CuiDong), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(HuiFu), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(KeXue), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(ZiShi), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(ShiHai1), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(ShiHai2), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(ShiHai3), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(YaoHua1), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(YaoHua2), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(YaoHua3), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(YaoHua4), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(YaoHua5), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(YaoHua6), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(YaoHua7), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(YaoHua8), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(YaoHua9), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(XueQi), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(XueQi1), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(XueQi2), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(XueQi3), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
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
[RegisterOwnedCardKeyword(nameof(YiChu), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(ZhuiJi), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(XuYing), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(LianLi), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(NingYing), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
public sealed class GuZhenRenKeywords
{
    public static readonly CardKeyword Unique = Create(nameof(Unique));
    public static readonly CardKeyword XianGu = Create(nameof(XianGu));
    public static readonly CardKeyword CuiDong = Create(nameof(CuiDong));
    public static readonly CardKeyword HuiFu = Create(nameof(HuiFu));
    public static readonly CardKeyword KeXue = Create(nameof(KeXue));
    public static readonly CardKeyword ZiShi = Create(nameof(ZiShi));
    public static readonly CardKeyword ShiHai1 = Create(nameof(ShiHai1));
    public static readonly CardKeyword ShiHai2 = Create(nameof(ShiHai2));
    public static readonly CardKeyword ShiHai3 = Create(nameof(ShiHai3));
    public static readonly CardKeyword YaoHua1 = Create(nameof(YaoHua1));
    public static readonly CardKeyword YaoHua2 = Create(nameof(YaoHua2));
    public static readonly CardKeyword YaoHua3 = Create(nameof(YaoHua3));
    public static readonly CardKeyword YaoHua4 = Create(nameof(YaoHua4));
    public static readonly CardKeyword YaoHua5 = Create(nameof(YaoHua5));
    public static readonly CardKeyword YaoHua6 = Create(nameof(YaoHua6));
    public static readonly CardKeyword YaoHua7 = Create(nameof(YaoHua7));
    public static readonly CardKeyword YaoHua8 = Create(nameof(YaoHua8));
    public static readonly CardKeyword YaoHua9 = Create(nameof(YaoHua9));
    public static readonly CardKeyword XueQi = Create(nameof(XueQi));

    /// <summary>
    /// 血气寄生的触发次数版关键词：X 为宿主牌触发次数。
    /// 按转数触发 1～3 次，分别打上 血气1/血气2/血气3。
    /// </summary>
    public static readonly CardKeyword XueQi1 = Create(nameof(XueQi1));

    public static readonly CardKeyword XueQi2 = Create(nameof(XueQi2));

    public static readonly CardKeyword XueQi3 = Create(nameof(XueQi3));
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
    public static readonly CardKeyword YiChu = Create(nameof(YiChu));
    public static readonly CardKeyword ZhuiJi = Create(nameof(ZhuiJi));
    public static readonly CardKeyword XuYing = Create(nameof(XuYing));
    public static readonly CardKeyword LianLi = Create(nameof(LianLi));
    public static readonly CardKeyword NingYing = Create(nameof(NingYing));
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
            Create("XianYuan"),
            Create("ZheGuang"),
            Create("ZhaoPo"),
            Create("JuGuang"),
            Create("DingGuang"),
            Create("LiuGuang"),
            Create("PoJing"),
            Create("XueYuan"),
            Create("XueJi"),
            Create("YiHai"),
            Create("XueLu"),
            Create("LiuXue"),
            Create("XueYin"),
            Create("XueHe"),
        };

    public static IReadOnlySet<CardKeyword> YaoHuaKeywords { get; } =
        new HashSet<CardKeyword>
        {
            YaoHua1, YaoHua2, YaoHua3,
            YaoHua4, YaoHua5, YaoHua6,
            YaoHua7, YaoHua8, YaoHua9,
        };

    /// <summary>
    /// 本模组注册的全部自定义关键词。
    /// UI 可见性补丁只过滤这个集合，不影响游戏本体的消耗、保留等提示。
    /// </summary>
    public static IReadOnlySet<CardKeyword> OwnedKeywords { get; } =
        new HashSet<CardKeyword>
        {
            Unique, XianGu, CuiDong, HuiFu,
            KeXue, ZiShi,
            ShiHai1, ShiHai2, ShiHai3,
            YaoHua1, YaoHua2, YaoHua3,
            YaoHua4, YaoHua5, YaoHua6,
            YaoHua7, YaoHua8, YaoHua9,
            XueQi, XueQi1, XueQi2, XueQi3,
            YueXiang, CanYue,
            YingYue, ManYue, XueTai, TaiDong, PoTai, FuHua,
            TunJi, ZongEDu, YiChu, ZhuiJi,
            XuYing, LianLi, NingYing,
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

    public static CardKeyword GetShiHaiKeyword(int maximum) =>
        maximum switch
        {
            1 => ShiHai1,
            2 => ShiHai2,
            3 => ShiHai3,
            _ => throw new ArgumentOutOfRangeException(
                nameof(maximum),
                maximum,
                "嗜骸选择上限必须位于一至三张。"
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
