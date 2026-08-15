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


