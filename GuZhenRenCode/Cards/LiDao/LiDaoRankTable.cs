using MegaCrit.Sts2.Core.Models;

namespace GuZhenRen.Cards.LiDao;

/// <summary>设计文档中九转数值的集中查表入口。</summary>
public static class LiDaoRankTable
{
    private static int At(int rank, params int[] values) =>
        values[Math.Clamp(rank, 1, 9) - 1];

    public static int Recovery(int rank) => rank switch
    {
        <= 5 => 2,
        <= 8 => 3,
        _ => 4,
    };

    public static int CondenseChanceGain(int rank) => rank switch
    {
        <= 5 => 10,
        <= 8 => 15,
        _ => 20,
    };

    public static int BaiZhiChance(int rank) =>
        At(rank, 30, 32, 35, 38, 40, 45, 48, 55, 60);

    public static int BaiZhiDamage(int rank) =>
        At(rank, 5, 6, 7, 8, 10, 12, 14, 17, 20);

    public static int FeiXiongChance(int rank) =>
        At(rank, 18, 20, 22, 25, 28, 30, 33, 36, 40);

    public static int FeiXiongDamage(int rank) =>
        At(rank, 10, 12, 14, 16, 19, 24, 28, 34, 40);

    public static int FeiXiongDivineMight(int rank) =>
        At(rank, 0, 0, 0, 0, 0, 6, 8, 10, 12);

    public static int EChance(int rank) =>
        At(rank, 25, 27, 30, 32, 35, 38, 40, 43, 45);

    public static int EDamage(int rank) =>
        At(rank, 3, 4, 4, 5, 5, 6, 7, 8, 8);

    public static int EHits(int rank) =>
        At(rank, 2, 2, 2, 2, 3, 3, 3, 4, 4);

    public static int QingNiuChance(int rank) =>
        At(rank, 35, 37, 40, 42, 45, 48, 50, 53, 55);

    public static int QingNiuDamage(int rank) =>
        At(rank, 4, 5, 6, 7, 8, 10, 12, 14, 17);

    public static int QingNiuBlock(int rank) =>
        At(rank, 2, 2, 3, 4, 5, 6, 7, 9, 11);

    public static int ShiGuiChance(int rank) =>
        At(rank, 30, 32, 35, 38, 40, 43, 45, 48, 50);

    public static int ShiGuiBlock(int rank) =>
        At(rank, 4, 5, 6, 7, 9, 11, 13, 15, 18);

    public static int BaiShouChance(int rank) =>
        At(rank, 25, 28, 30, 33, 35, 38, 40, 45, 50);

    public static int BaiShouEffectPercent(int rank) =>
        At(rank, 60, 70, 80, 85, 90, 100, 100, 100, 100);

    public static int FullForcePercent(int rank) =>
        At(rank, 100, 100, 80, 90, 100, 110, 120, 130, 140);

    public static int ZiLiHealingCap(int rank) =>
        At(rank, 1, 2, 3, 4, 5, 6, 7, 9, 12);

    public static LiDaoBeastKind GetBeastKind(CardModel card) => card switch
    {
        BaiZhiGu => LiDaoBeastKind.BaiZhi,
        FeiXiongZhiLiGu => LiDaoBeastKind.FeiXiong,
        ELiGu => LiDaoBeastKind.E,
        QingNiuLaoLiGu => LiDaoBeastKind.QingNiu,
        ShiGuiLiGu => LiDaoBeastKind.ShiGui,
        _ => throw new ArgumentException(
            $"{card.GetType().Name} 不是基础兽力蛊。",
            nameof(card)
        ),
    };
}
