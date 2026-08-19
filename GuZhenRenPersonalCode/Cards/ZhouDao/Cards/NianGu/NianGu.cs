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
public sealed class NianGu : AbstractZhouDaoCompanionGuCard
{
    public override Type CompanionCardType => typeof(NianNianSuiSui);

    public override int RecoveryDelayTurns => GuRank switch
    {
        <= 5 => 2,
        <= 8 => 3,
        _ => 4,
    };

    public NianGu() : base(CardRarity.Common)
    {
    }

    protected override void AddExtraArgsToDescription(
        LocString description
    )
    {
        base.AddExtraArgsToDescription(description);

        int current = CombatState != null
            ? ZhouDaoPowerSystem.GetNianHua(Owner)
            : 0;
        int gain = GuRank switch
        {
            <= 2 => 1,
            <= 4 => 2,
            5 => current <= 3 ? 3 : 2,
            <= 7 => 3,
            _ => 4,
        };

        description.Add("CurrentGain", gain);
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        int current = ZhouDaoPowerSystem.GetNianHua(Owner);
        int gain = GuRank switch
        {
            <= 2 => 1,
            <= 4 => 2,
            5 => current <= 3 ? 3 : 2,
            <= 7 => 3,
            _ => 4,
        };

        NianHuaGainResult result = await ZhouDaoPowerSystem.GainNianHua(
            choiceContext,
            this,
            gain
        );
        if (GuRank >= 9 && result.SuiManCount > 0)
        {
            await ZhouDaoPowerSystem.GainNianHua(
                choiceContext,
                this,
                1
            );
        }
    }
}
