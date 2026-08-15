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
// 六转以上回溯蛊 ×1 + 六转以上年蛊 ×1 → 宙锚仙蛊
[HeLianRecipe(typeof(HuiSuGu), typeof(NianGu), MinimumMaterialRank = 6)]
public sealed class ZhouMaoXianGu : AbstractZhouDaoCompanionGuCard, ICardRewardExcluded
{
    public override int MinimumAvailableGuRank => 6;
    public override Type CompanionCardType => typeof(ZhouMao);

    public override int RecoveryDelayTurns => GuRank == 6 ? 3 : 4;

    public ZhouMaoXianGu() : base(CardRarity.Rare)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        bool alreadySuiMan = ZhouDaoPowerSystem.HasSuiManThisTurn(Owner);
        int gain = GuRank >= 8 ? 3 : 2;
        NianHuaGainResult result = await ZhouDaoPowerSystem.GainNianHua(
            choiceContext,
            this,
            gain
        );

        if ((GuRank == 7 && alreadySuiMan) ||
            (GuRank >= 9 && result.SuiManCount > 0))
        {
            await CardPileCmd.Draw(choiceContext, 1, Owner);
        }
    }
}
