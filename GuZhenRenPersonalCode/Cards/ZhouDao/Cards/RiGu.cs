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
public sealed class RiGu : AbstractZhouDaoGuCard
{
    public override int MinimumAvailableGuRank => 3;

    public override int RecoveryDelayTurns => GuRank switch
    {
        <= 4 => 1,
        <= 7 => 2,
        _ => 3,
    };

    public RiGu() : base(CardRarity.Common)
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
            3 => 2,
            4 => current <= 2 ? 3 : 2,
            5 or 6 => 3,
            7 or 8 => 4,
            _ => 5,
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
            3 => 2,
            4 => current <= 2 ? 3 : 2,
            5 or 6 => 3,
            7 or 8 => 4,
            _ => 5,
        };
        NianHuaGainResult result = await ZhouDaoPowerSystem.GainNianHua(
            choiceContext,
            this,
            gain
        );

        if (GuRank == 6 && result.SuiManCount > 0)
        {
            await CardPileCmd.Draw(choiceContext, 1, Owner);
        }
        if (GuRank >= 8 && result.SuiManCount > 0)
        {
            await ZhouDaoPowerSystem.GainNianHua(
                choiceContext,
                this,
                1
            );
        }
    }
}
