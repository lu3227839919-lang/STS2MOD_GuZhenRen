using MegaCrit.Sts2.Core.Entities.Cards;
using STS2RitsuLib.Interop.AutoRegistration;

using GuZhenRen.Cards;
using GuZhenRen.Characters;

namespace GuZhenRen.Cards.HeLian;

/// <summary>
/// 所有专属合练蛊虫的公共父类。
///
/// 公共规则：
/// 1. 注册到角色普通卡池，不再使用独立合练卡池；
/// 2. 是否进入普通卡牌奖励由具体卡牌显式决定；
/// 3. 合练生成时的转数等于配方全部材料的最高转数；
/// 4. 不允许被战斗内随机生成。
/// </summary>
[RegisterCard(
    typeof(GuZhenRenGuCardPool),
    Inherit = true
)]
public abstract class AbstractHeLianGuCard
    : AbstractGuWormCard
{
    protected AbstractHeLianGuCard(
        int baseCost,
        CardType type,
        CardRarity rarity,
        TargetType target,
        bool showInCardLibrary = true
    )
        : base(
            baseCost,
            type,
            rarity,
            target,
            showInCardLibrary
        )
    {
        SetGuRank(MinimumGuRank);
    }

    public override bool CanBeGeneratedInCombat =>
        false;
}
