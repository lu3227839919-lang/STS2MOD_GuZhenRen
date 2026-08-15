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
