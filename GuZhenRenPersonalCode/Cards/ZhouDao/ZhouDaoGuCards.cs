// ============================================================================
// 中文维护说明
// 文件职责：实现蛊真人卡牌、衍生牌及其战斗结算逻辑。
// 主要类型：AbstractZhouDaoGuCard、AbstractZhouDaoCompanionGuCard。
// 实现要点：公开成员构成该模块的稳定协作面；修改签名时应同步检查注册点与调用方。
// 维护约定：修改数值或关键词时同步检查 zhs/eng 本地化；异步战斗效果必须 await。
// ============================================================================
using GuZhenRen.Cards.HeLian;
using GuZhenRen.Characters;
using GuZhenRen.Combat;
using GuZhenRen.Powers.ZhouDao;

using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;

namespace GuZhenRen.Cards.ZhouDao;

public abstract class AbstractZhouDaoGuCard : AbstractGuWormCard
{
    protected AbstractZhouDaoGuCard(
        CardRarity rarity,
        TargetType target = TargetType.Self
    ) : base(0, CardType.Skill, rarity, target)
    {
        SetDao(Dao.ZhouDao);
        this.SecondaryCosts().Set(YuanQiSystem.ResourceId, 1);
    }
}

public abstract class AbstractZhouDaoCompanionGuCard :
    AbstractZhouDaoGuCard,
    IZhouDaoCompanionGuCard
{
    public abstract Type CompanionCardType { get; }

    protected AbstractZhouDaoCompanionGuCard(CardRarity rarity)
        : base(rarity)
    {
    }

}
