namespace GuZhenRen.Cards.LiDao;

/// <summary>
/// 力道伴生牌（沉肩冲/飞熊撞/绞摔/牛角顶/沉桩/苦练/调息运力/运力/百兽架势）
/// 九转数值的集中查表入口，与《力道补充文档》逐转效果表一一对应。
/// 索引规则：At(rank, ...) 的第 i 个值对应 i+1 转（1-9 转）。
/// </summary>
public static class LiDaoCompanionRankTable
{
    private static int At(int rank, params int[] values) =>
        values[Math.Clamp(rank, 1, 9) - 1];

    // ============ 1. 沉肩冲・白豕蛊 ============
    public static int ChenJianChongDamage(int rank) =>
        At(rank, 7, 8, 9, 10, 11, 13, 14, 16, 18);

    /// <summary>三至六转：本回合第一张攻击时额外伤害；其余转数为 0。</summary>
    public static int ChenJianChongFirstAttackBonus(int rank) =>
        At(rank, 0, 0, 2, 2, 3, 3, 0, 0, 0);

    /// <summary>七至九转：目标没有格挡时额外伤害；其余转数为 0。</summary>
    public static int ChenJianChongNoBlockBonus(int rank) =>
        At(rank, 0, 0, 0, 0, 0, 0, 3, 4, 5);

    // ============ 2. 飞熊撞・飞熊之力蛊 ============
    public static int FeiXiongZhuangDamage(int rank) =>
        At(rank, 8, 9, 10, 11, 13, 14, 16, 17, 19);

    /// <summary>一至五转：目标有格挡时额外普通伤害；其余转数为 0。</summary>
    public static int FeiXiongZhuangBlockBonus(int rank) =>
        At(rank, 3, 3, 4, 5, 6, 0, 0, 0, 0);

    /// <summary>六至九转：目标有格挡时额外神力伤害；其余转数为 0。</summary>
    public static int FeiXiongZhuangDivineMight(int rank) =>
        At(rank, 0, 0, 0, 0, 0, 6, 7, 8, 9);

    /// <summary>八至九转：对其他敌人造成的震地余波伤害；其余转数为 0。</summary>
    public static int FeiXiongZhuangQuake(int rank) =>
        At(rank, 0, 0, 0, 0, 0, 0, 0, 4, 6);

    // ============ 3. 绞摔・鳄力蛊 ============
    public static int JiaoShuaiDamage(int rank) =>
        At(rank, 4, 5, 4, 5, 5, 6, 6, 7, 8);

    public static int JiaoShuaiHits(int rank) =>
        At(rank, 2, 2, 2, 2, 3, 3, 3, 4, 4);

    /// <summary>三至四转：最后一段额外伤害；其余转数为 0。</summary>
    public static int JiaoShuaiLastHitBonus(int rank) =>
        At(rank, 0, 0, 2, 2, 0, 0, 0, 0, 0);

    /// <summary>七转起：目标中途死亡时剩余段数追击其他敌人。</summary>
    public static bool JiaoShuaiPursues(int rank) => rank >= 7;

    // ============ 4. 牛角顶・青牛劳力蛊 ============
    public static int NiuJiaoDingDamage(int rank) =>
        At(rank, 6, 7, 8, 9, 10, 11, 12, 14, 16);

    public static int NiuJiaoDingBlock(int rank) =>
        At(rank, 3, 3, 4, 5, 6, 7, 8, 10, 11);

    /// <summary>八至九转：本场首次打出时额外格挡；其余转数为 0。</summary>
    public static int NiuJiaoDingFirstTimeBonus(int rank) =>
        At(rank, 0, 0, 0, 0, 0, 0, 0, 3, 3);

    /// <summary>九转：若本回合已有其他虚影显化过，再获得额外格挡；其余转数为 0。</summary>
    public static int NiuJiaoDingPhantomLinkBonus(int rank) =>
        At(rank, 0, 0, 0, 0, 0, 0, 0, 0, 5);

    // ============ 5. 沉桩・石龟力蛊 ============
    public static int ChenZhuangBlock(int rank) =>
        At(rank, 8, 9, 10, 11, 12, 13, 14, 15, 16);

    /// <summary>本回合打过攻击牌时的额外格挡。</summary>
    public static int ChenZhuangAttackBonus(int rank) =>
        At(rank, 2, 2, 3, 3, 4, 4, 5, 5, 6);

    /// <summary>五至八转：打出前没有格挡时再获得的格挡；九转改为基础格挡提高 50%。</summary>
    public static int ChenZhuangNoBlockBonus(int rank) =>
        At(rank, 0, 0, 0, 0, 2, 3, 4, 5, 0);

    // ============ 6. 苦练・苦力蛊 ============
    public static int KuLianBlock(int rank) =>
        At(rank, 12, 13, 14, 15, 16, 18, 19, 21, 23);

    /// <summary>五转起：每层苦境额外格挡；其余转数为 0。</summary>
    public static int KuLianHardshipBonus(int rank) =>
        At(rank, 0, 0, 0, 0, 1, 1, 1, 2, 2);

    // ============ 7. 调息运力・自力更生蛊 ============
    public static int TiaoXiYunLiBlock(int rank) =>
        At(rank, 7, 8, 9, 10, 11, 12, 13, 14, 16);

    /// <summary>拥有常驻虚影时的额外格挡。</summary>
    public static int TiaoXiYunLiPhantomBonus(int rank) =>
        At(rank, 3, 3, 4, 4, 5, 5, 6, 6, 7);

    /// <summary>
    /// 六至七转：拥有常驻虚影时回复 1 生命；八转：至少 2 种常驻虚影时回复 2；
    /// 九转：拥有 1/2/3 种常驻虚影时分别回复 1/2/3。其余转数 0。
    /// </summary>
    public static int TiaoXiYunLiHeal(int rank, int phantomKinds) => rank switch
    {
        6 or 7 => phantomKinds >= 1 ? 1 : 0,
        8 => phantomKinds >= 2 ? 2 : 0,
        9 => Math.Min(phantomKinds, 3),
        _ => 0,
    };

    /// <summary>六转及以上才可能回复生命。</summary>
    public static bool TiaoXiYunLiCanHeal(int rank) => rank >= 6;

    // ============ 8. 运力・全力以赴蛊 ============
    public static int YunLiBlock(int rank) =>
        At(rank, 5, 6, 6, 7, 8, 9, 10, 11, 12);

    public static int YunLiVigor(int rank) =>
        At(rank, 3, 3, 4, 4, 5, 5, 6, 6, 7);

    /// <summary>
    /// 六至七转：拥有常驻虚影时额外 1 活力；八至九转：至少 2 种常驻虚影时额外 2。其余 0。
    /// </summary>
    public static int YunLiPhantomVigorBonus(int rank, int phantomKinds) => rank switch
    {
        6 or 7 => phantomKinds >= 1 ? 1 : 0,
        8 or 9 => phantomKinds >= 2 ? 2 : 0,
        _ => 0,
    };

    // ============ 9. 百兽架势・百兽力蛊 ============
    public static int BaiShouJiaShiDamage(int rank) =>
        At(rank, 7, 8, 9, 10, 11, 12, 13, 15, 17);

    /// <summary>拥有至少 2 种常驻虚影时获得的格挡。</summary>
    public static int BaiShouJiaShiBlock(int rank) =>
        At(rank, 4, 4, 5, 5, 6, 7, 8, 9, 11);

    /// <summary>五转起：拥有至少 3 种常驻虚影时额外伤害；其余 0。</summary>
    public static int BaiShouJiaShiExtraDamage(int rank) =>
        At(rank, 0, 0, 0, 0, 2, 2, 3, 4, 5);

    /// <summary>八转起：拥有至少 4 种常驻虚影时再获得格挡；其余 0。</summary>
    public static int BaiShouJiaShiBlockFour(int rank) =>
        At(rank, 0, 0, 0, 0, 0, 0, 0, 3, 4);
}
