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
[RegisterOwnedCardKeyword(nameof(ZongEDu), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(YiChu), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(ZhuiJi), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(PoShi), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(XuYing), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(LianLi), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(ZheGuangCore), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(JuGuangCore), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(TiaoGuCore), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(YueHuaCore), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
[RegisterOwnedCardKeyword(nameof(ShiXue), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
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
    public static readonly CardKeyword ZongEDu = Create(nameof(ZongEDu));
    public static readonly CardKeyword YiChu = Create(nameof(YiChu));
    public static readonly CardKeyword ZhuiJi = Create(nameof(ZhuiJi));
    public static readonly CardKeyword PoShi = Create(nameof(PoShi));
    public static readonly CardKeyword XuYing = Create(nameof(XuYing));
    public static readonly CardKeyword LianLi = Create(nameof(LianLi));
    public static readonly CardKeyword ZheGuangCore = Create(nameof(ZheGuangCore));
    public static readonly CardKeyword JuGuangCore = Create(nameof(JuGuangCore));
    public static readonly CardKeyword TiaoGuCore = Create(nameof(TiaoGuCore));
    public static readonly CardKeyword YueHuaCore = Create(nameof(YueHuaCore));
    public static readonly CardKeyword ShiXue = Create(nameof(ShiXue));
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
            Create("YaoHua1"),
            Create("YaoHua2"),
            Create("YaoHua3"),
            Create("YaoHua4"),
            Create("YaoHua5"),
            Create("YaoHua6"),
            Create("YaoHua7"),
            Create("YaoHua8"),
            Create("YaoHua9"),
            Create("ZhaoXi"),
            Create("YanGuang"),
            Create("YingGuang"),
            Create("ShouHui"),
            Create("PoHui"),
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
            ZongEDu, YiChu, ZhuiJi, PoShi,
            XuYing, LianLi, ShangShi,
            ZheGuangCore, JuGuangCore, TiaoGuCore, YueHuaCore,
            ShiXue,
            NianHua, SuiMan, HuanBu, XiYing,
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
