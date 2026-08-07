// 《杀戮尖塔2》原生 CardTag 类型。
using MegaCrit.Sts2.Core.Entities.Cards;

// RitsuLib 提供的自定义卡牌标签扩展方法。
using STS2RitsuLib.CardTags;

// RitsuLib 的 Mod 内容注册表。
using STS2RitsuLib.Content;

// RitsuLib 自动注册特性。
using STS2RitsuLib.Interop.AutoRegistration;

namespace GuZhenRen.Cards;

/// <summary>
/// 蛊真人模组全部自定义卡牌标签的集中入口。
///
/// 每个标签都需要完成两件事：
///
/// 1. 使用 RegisterOwnedCardTag 告诉 RitsuLib 注册该标签；
/// 2. 使用 ModContentRegistry 取得游戏运行时使用的 CardTag。
/// </summary>

// ==========================================================================
//  特殊分类标签
// ==========================================================================

[RegisterOwnedCardTag(nameof(BenMingGu))]
[RegisterOwnedCardTag(nameof(XianGu))]

// 杀招与蛊屋。
[RegisterOwnedCardTag(nameof(ShaZhao))]
[RegisterOwnedCardTag(nameof(XianGuWu))]
[RegisterOwnedCardTag(nameof(FanGuWu))]

// ==========================================================================
//  十九种流派标签
// ==========================================================================

[RegisterOwnedCardTag(nameof(GuangDao))]
[RegisterOwnedCardTag(nameof(YanDao))]
[RegisterOwnedCardTag(nameof(LiDao))]
[RegisterOwnedCardTag(nameof(JinDao))]
[RegisterOwnedCardTag(nameof(TouDao))]
[RegisterOwnedCardTag(nameof(MuDao))]
[RegisterOwnedCardTag(nameof(ShiDao))]
[RegisterOwnedCardTag(nameof(ShaDao))]
[RegisterOwnedCardTag(nameof(GuDao))]
[RegisterOwnedCardTag(nameof(LuDao))]
[RegisterOwnedCardTag(nameof(ZhiDao))]
[RegisterOwnedCardTag(nameof(BianHuaDao))]
[RegisterOwnedCardTag(nameof(YinYangDao))]
[RegisterOwnedCardTag(nameof(JianDao))]
[RegisterOwnedCardTag(nameof(XueDao))]
[RegisterOwnedCardTag(nameof(YunDao))]
[RegisterOwnedCardTag(nameof(FengDao))]
[RegisterOwnedCardTag(nameof(ZhouDao))]
[RegisterOwnedCardTag(nameof(TuDao))]
public sealed class GuZhenRenTags
{
    // =====================================================================
    //  特殊分类标签
    // =====================================================================

    /// <summary>
    /// 本命蛊标签。
    /// </summary>
    public static readonly CardTag BenMingGu =
        Create(nameof(BenMingGu));

    /// <summary>
    /// 仙蛊标签。
    /// </summary>
    public static readonly CardTag XianGu =
        Create(nameof(XianGu));

    /// <summary>
    /// 杀招标签。
    ///
    /// 所有继承 AbstractShaZhaoCard 的卡牌都会自动拥有该标签。
    /// </summary>
    public static readonly CardTag ShaZhao =
        Create(nameof(ShaZhao));

    /// <summary>
    /// 仙蛊屋标签。
    /// </summary>
    public static readonly CardTag XianGuWu =
        Create(nameof(XianGuWu));

    /// <summary>
    /// 凡蛊屋标签。
    /// </summary>
    public static readonly CardTag FanGuWu =
        Create(nameof(FanGuWu));

    // =====================================================================
    //  十九种流派标签
    // =====================================================================

    public static readonly CardTag GuangDao =
        Create(nameof(GuangDao));

    public static readonly CardTag YanDao =
        Create(nameof(YanDao));

    public static readonly CardTag LiDao =
        Create(nameof(LiDao));

    public static readonly CardTag JinDao =
        Create(nameof(JinDao));

    public static readonly CardTag TouDao =
        Create(nameof(TouDao));

    public static readonly CardTag MuDao =
        Create(nameof(MuDao));

    public static readonly CardTag ShiDao =
        Create(nameof(ShiDao));

    public static readonly CardTag ShaDao =
        Create(nameof(ShaDao));

    public static readonly CardTag GuDao =
        Create(nameof(GuDao));

    public static readonly CardTag LuDao =
        Create(nameof(LuDao));

    public static readonly CardTag ZhiDao =
        Create(nameof(ZhiDao));

    public static readonly CardTag BianHuaDao =
        Create(nameof(BianHuaDao));

    public static readonly CardTag YinYangDao =
        Create(nameof(YinYangDao));

    public static readonly CardTag JianDao =
        Create(nameof(JianDao));

    public static readonly CardTag XueDao =
        Create(nameof(XueDao));

    public static readonly CardTag YunDao =
        Create(nameof(YunDao));

    public static readonly CardTag FengDao =
        Create(nameof(FengDao));

    public static readonly CardTag ZhouDao =
        Create(nameof(ZhouDao));

    public static readonly CardTag TuDao =
        Create(nameof(TuDao));

    // =====================================================================
    //  标签创建辅助
    // =====================================================================

    /// <summary>
    /// 根据本地标签名创建当前 Mod 的全限定 CardTag。
    /// </summary>
    private static CardTag Create(string localName)
    {
        return ModContentRegistry
            .GetQualifiedCardTagId(
                Entry.ModId,
                localName
            )
            .GetModCardTag();
    }

    /// <summary>
    /// 该类只保存静态标签，不允许创建实例。
    /// </summary>
    private GuZhenRenTags()
    {
    }
}
