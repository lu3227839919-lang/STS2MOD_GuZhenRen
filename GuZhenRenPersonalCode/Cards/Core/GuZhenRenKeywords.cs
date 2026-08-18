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
[RegisterOwnedCardKeyword(nameof(CuiDong), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(KeXue), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(XueJiCost), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(LianHai1), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(LianHai2), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(LianHai3), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(XueQiCore), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(XueYueCore), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(XueTaiCore), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(YaoHua1), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(YaoHua2), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(YaoHua3), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(YaoHua4), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(YaoHua5), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(YaoHua6), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(YaoHua7), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(YaoHua8), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(YaoHua9), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(ZongEDu), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(YiChu), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(ZhuiJi), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(PoShi), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(XuYing), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(LianLi), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(ShangShi), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(NianHua), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(SuiMan), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(HuanBu), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(XiYing), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
public sealed class GuZhenRenKeywords
{
    public static readonly CardKeyword Unique = Create(nameof(Unique));
    public static readonly CardKeyword CuiDong = Create(nameof(CuiDong));
    public static readonly CardKeyword KeXue = Create(nameof(KeXue));
    public static readonly CardKeyword XueJiCost = Create(nameof(XueJiCost));
    public static readonly CardKeyword LianHai1 = Create(nameof(LianHai1));
    public static readonly CardKeyword LianHai2 = Create(nameof(LianHai2));
    public static readonly CardKeyword LianHai3 = Create(nameof(LianHai3));
    public static readonly CardKeyword XueQiCore = Create(nameof(XueQiCore));
    public static readonly CardKeyword XueYueCore = Create(nameof(XueYueCore));
    public static readonly CardKeyword XueTaiCore = Create(nameof(XueTaiCore));
    public static readonly CardKeyword YaoHua1 = Create(nameof(YaoHua1));
    public static readonly CardKeyword YaoHua2 = Create(nameof(YaoHua2));
    public static readonly CardKeyword YaoHua3 = Create(nameof(YaoHua3));
    public static readonly CardKeyword YaoHua4 = Create(nameof(YaoHua4));
    public static readonly CardKeyword YaoHua5 = Create(nameof(YaoHua5));
    public static readonly CardKeyword YaoHua6 = Create(nameof(YaoHua6));
    public static readonly CardKeyword YaoHua7 = Create(nameof(YaoHua7));
    public static readonly CardKeyword YaoHua8 = Create(nameof(YaoHua8));
    public static readonly CardKeyword YaoHua9 = Create(nameof(YaoHua9));
    public static readonly CardKeyword ZongEDu = Create(nameof(ZongEDu));
    public static readonly CardKeyword YiChu = Create(nameof(YiChu));
    public static readonly CardKeyword ZhuiJi = Create(nameof(ZhuiJi));
    public static readonly CardKeyword PoShi = Create(nameof(PoShi));
    public static readonly CardKeyword XuYing = Create(nameof(XuYing));
    public static readonly CardKeyword LianLi = Create(nameof(LianLi));
    public static readonly CardKeyword ShangShi = Create(nameof(ShangShi));
    public static readonly CardKeyword NianHua = Create(nameof(NianHua));
    public static readonly CardKeyword SuiMan = Create(nameof(SuiMan));
    public static readonly CardKeyword HuanBu = Create(nameof(HuanBu));
    public static readonly CardKeyword XiYing = Create(nameof(XiYing));
    /// <summary>
    /// 旧版本可能已经把这些展示关键词写进卡牌实例或多人快照。
    /// 保留对应 ID 仅用于清理；它们不再注册，也不会重新加入卡牌。
    /// </summary>
    public static IReadOnlySet<CardKeyword> RemovedDisplayKeywords { get; } =
        new HashSet<CardKeyword>
        {
            Create("XianGu"),
            Create("HuiFu"),
            Create("ZiShi"),
            Create("ShiHai1"),
            Create("ShiHai2"),
            Create("ShiHai3"),
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
            Create("XueQi"),
            Create("XueQi1"),
            Create("XueQi2"),
            Create("XueQi3"),
            Create("YueXiang"),
            Create("CanYue"),
            Create("YingYue"),
            Create("ManYue"),
            Create("XueTai"),
            Create("TaiDong"),
            Create("PoTai"),
            Create("FuHua"),
            Create("TunJi"),
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
            Unique, CuiDong,
            KeXue, XueJiCost,
            LianHai1, LianHai2, LianHai3,
            XueQiCore, XueYueCore, XueTaiCore,
            YaoHua1, YaoHua2, YaoHua3,
            YaoHua4, YaoHua5, YaoHua6,
            YaoHua7, YaoHua8, YaoHua9,
            ZongEDu, YiChu, ZhuiJi, PoShi,
            XuYing, LianLi, ShangShi,
            NianHua, SuiMan, HuanBu, XiYing,
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

    public static CardKeyword GetLianHaiKeyword(int maximum) =>
        maximum switch
        {
            1 => LianHai1,
            2 => LianHai2,
            3 => LianHai3,
            _ => throw new ArgumentOutOfRangeException(
                nameof(maximum),
                maximum,
                "炼骸上限必须位于一至三张。"
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
