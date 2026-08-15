using GuZhenRen.Characters;

using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRen.Cards;

/// <summary>
/// 蛊真人战斗内生成牌的公共父类。
///
/// 伴生牌、战斗衍生牌和杀招推演牌都只是由战斗流程临时创建的牌：
/// 它们使用蛊真人公共卡池完成模型注册与资源加载，但不应进入普通
/// 奖励，也不应被原版随机战斗生成流程选中。把这些规则放在 Core 的公共
/// 父类中，避免每种伴生/衍生牌重复声明同一套池和过滤标记。
/// </summary>
public abstract class AbstractGuZhenRenGeneratedCard
    : AbstractGuZhenRenCard,
      ICardRewardExcluded
{
    /// <summary>
    /// 生成牌统一注册到公共卡池，便于战斗创建和卡牌资源查找；普通蛊虫
    /// 与合练蛊仍注册到 GuZhenRenGuCardPool。
    /// </summary>
    public override CardPoolModel Pool =>
        ModelDb.CardPool<GuZhenRenCardPool>();

    /// <summary>
    /// 生成牌只允许由模组自己的战斗流程创建。
    /// </summary>
    public override bool CanBeGeneratedInCombat => false;

    protected AbstractGuZhenRenGeneratedCard(
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
    }
}
