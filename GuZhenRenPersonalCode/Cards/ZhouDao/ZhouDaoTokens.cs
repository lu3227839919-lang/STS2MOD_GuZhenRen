// ============================================================================
// 中文维护说明
// 文件职责：定义同一玩法分支共享的衍生牌基类与令牌约定。
// 主要类型：AbstractZhouDaoToken。
// 实现要点：公开成员构成该模块的稳定协作面；修改签名时应同步检查注册点与调用方。
// 维护约定：修改数值或关键词时同步检查 zhs/eng 本地化；异步战斗效果必须 await。
// ============================================================================
using GuZhenRen.Characters;
using GuZhenRen.Powers.ZhouDao;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.ZhouDao;

public abstract class AbstractZhouDaoToken :
    AbstractGuZhenRenGeneratedCard
{
    public override int MaxUpgradeLevel => 0;
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Exhaust, GuZhenRenKeywords.NianHua, GuZhenRenKeywords.SuiMan];

    protected AbstractZhouDaoToken()
        : base(0, CardType.Skill, CardRarity.Token, TargetType.Self)
    {
        SetDao(Dao.ZhouDao);
    }
}


