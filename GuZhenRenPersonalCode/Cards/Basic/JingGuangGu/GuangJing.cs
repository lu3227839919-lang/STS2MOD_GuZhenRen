using GuZhenRen.Characters;
using GuZhenRen.Multiplayer;
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

namespace GuZhenRen.Cards.GuangDao;

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class GuangJing : AbstractLightExpansionToken
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(9m, ValueProp.Move),
        new DynamicVar("LightBonus", 3m),
    ];

    public override bool GainsBlock => true;

    public override CardAssetProfile AssetProfile =>
        global::GuZhenRen.Cards.CardImageCatalog.Create(GetType());

    public GuangJing() : base(1, CardType.Skill, TargetType.Self)
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

        if (GuRank >= 7 && cardPlay.PlayIndex == 0)
        {
            FanZhao reflected = GuGeneratedCardFactory.Create<FanZhao>(
                Owner,
                GuRank
            );
            await GuGeneratedCardFactory.AddToHandOrDiscard(
                reflected,
                Owner
            );
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3m);
        DynamicVars["LightBonus"].UpgradeValueBy(2m);
    }
}
