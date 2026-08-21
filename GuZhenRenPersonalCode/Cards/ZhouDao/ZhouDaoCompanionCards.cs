// ============================================================================
// 中文维护说明
// 文件职责：实现蛊真人卡牌、衍生牌及其战斗结算逻辑。
// 主要类型：AbstractZhouDaoCompanionCard。
// 实现要点：公开成员构成该模块的稳定协作面；修改签名时应同步检查注册点与调用方。
// 维护约定：修改数值或关键词时同步检查 zhs/eng 本地化；异步战斗效果必须 await。
// ============================================================================
using GuZhenRen.Cards.HeLian;
using GuZhenRen.Characters;
using GuZhenRen.Powers.ZhouDao;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.ZhouDao;

public abstract class AbstractZhouDaoCompanionCard :
    AbstractGuZhenRenGeneratedCard,
    IZhouDaoCompanionCard
{
    public abstract Type SourceGuType { get; }
    public override int MaxUpgradeLevel => 0;
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        base.CanonicalKeywords
            .Append(GuZhenRenKeywords.NianHua)
            .Append(GuZhenRenKeywords.SuiMan)
            .Distinct();

    protected AbstractZhouDaoCompanionCard()
        : base(1, CardType.Power, CardRarity.Common, TargetType.Self)
    {
        SetDao(Dao.ZhouDao);
    }

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        description.Add("RankCN", ToChineseNumber(GuRank));
    }
}


