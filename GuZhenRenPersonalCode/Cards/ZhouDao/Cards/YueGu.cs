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

[RegisterCard(typeof(GuZhenRenGuCardPool))]
public sealed class YueGu : AbstractZhouDaoGuCard
{
    public override int MinimumAvailableGuRank => 3;

    public override int RecoveryDelayTurns => GuRank switch
    {
        <= 5 => 2,
        <= 8 => 3,
        _ => 4,
    };

    public YueGu() : base(CardRarity.Common)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        (int now, int next, int following) = GuRank switch
        {
            3 => (1, 1, 0),
            4 => (1, 2, 0),
            5 => (2, 2, 0),
            6 => (2, 3, 0),
            7 => (2, 2, 2),
            8 => (2, 3, 3),
            _ => (3, 3, 3),
        };

        await ZhouDaoPowerSystem.GainNianHua(
            choiceContext,
            this,
            now
        );
        await YueGuDelayPower.ScheduleAsync(
            choiceContext,
            Owner,
            next,
            following,
            this
        );
    }
}
