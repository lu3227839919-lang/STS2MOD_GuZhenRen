using GuZhenRen.Characters;
using GuZhenRen.Powers.GuangDao;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.TuDao;

public abstract class AbstractYuPiToken
    : AbstractGuZhenRenGeneratedCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Exhaust];

    public override bool GainsBlock => true;

    protected override IEnumerable<CardTag> AdditionalCanonicalTags =>
        GuRank >= 6
            ? [GuZhenRenTags.GuangDao]
            : [];

    protected AbstractYuPiToken(int cost)
        : base(
            cost,
            CardType.Skill,
            CardRarity.Token,
            TargetType.Self
        )
    {
        SetDao(Dao.TuDao);
    }
}


