using GuZhenRen.Characters;
using GuZhenRen.Powers.GuangDao;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.HeLian;

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class NingJingHui : AbstractJingHuiToken
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(14m, ValueProp.Move),
        new DynamicVar("LightBonus", 5m),
        new DynamicVar("ZhaoPoBonus", 6m),
    ];

    public override bool GainsBlock => true;

    public NingJingHui() : base(1)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        await CreatureCmd.GainBlock(
            Owner.Creature,
            DynamicVars.Block,
            cardPlay
        );
        await PowerCmd.Apply<JuGuangPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["LightBonus"].IntValue,
            Owner.Creature,
            this
        );
        await PowerCmd.Apply<DingGuangChargePower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["ZhaoPoBonus"].IntValue,
            Owner.Creature,
            this
        );

        if (GuRank >= 7 && cardPlay.PlayIndex == 0)
        {
            FanHui returned = GuGeneratedCardFactory.Create<FanHui>(
                Owner,
                GuRank
            );
            await GuGeneratedCardFactory.AddToHandOrDiscard(
                returned,
                Owner
            );
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3m);
        DynamicVars["LightBonus"].UpgradeValueBy(2m);
        DynamicVars["ZhaoPoBonus"].UpgradeValueBy(2m);
    }
}
