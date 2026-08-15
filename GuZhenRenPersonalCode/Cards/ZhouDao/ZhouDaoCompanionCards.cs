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


