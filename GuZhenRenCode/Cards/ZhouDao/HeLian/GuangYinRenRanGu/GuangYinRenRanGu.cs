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

using GuZhenRen.Cards.ZhouDao;

namespace GuZhenRen.Cards.HeLian;

[RegisterCard(typeof(GuZhenRenGuCardPool))]
// 三转以上日蛊 ×1 + 三转以上月蛊 ×1 → 光阴荏苒蛊
[HeLianRecipe(typeof(RiGu), typeof(YueGu), MinimumMaterialRank = 3)]
public sealed class GuangYinRenRanGu : AbstractZhouDaoCompanionGuCard, ICardRewardExcluded
{
    public override Type CompanionCardType => typeof(GuangYinRenRan);

    public override int RecoveryDelayTurns => GuRank switch
    {
        <= 5 => 2,
        <= 8 => 3,
        _ => 4,
    };

    public GuangYinRenRanGu() : base(CardRarity.Uncommon)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        bool recovered = ZhouDaoPowerSystem.HasGuRecoveredThisTurn(Owner);
        int gain = GuRank switch
        {
            <= 2 => 1,
            <= 5 => 2,
            <= 8 => 3,
            _ => 4,
        };
        if (recovered && GuRank is 2 or 4 or 5)
        {
            gain++;
        }

        NianHuaGainResult result = await ZhouDaoPowerSystem.GainNianHua(
            choiceContext,
            this,
            gain
        );

        if (GuRank >= 8 && result.SuiManCount > 0)
        {
            await ZhouDaoPowerSystem.GainNianHua(
                choiceContext,
                this,
                1
            );
            if (GuRank >= 9)
            {
                await CardPileCmd.Draw(choiceContext, 1, Owner);
            }
        }
        else if (GuRank == 7 && recovered)
        {
            await CardPileCmd.Draw(choiceContext, 1, Owner);
        }
    }
}
