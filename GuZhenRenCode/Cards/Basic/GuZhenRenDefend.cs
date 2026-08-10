using GuZhenRen.Characters;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.Basic;

[RegisterCard(typeof(GuZhenRenCardPool))]
[RegisterCharacterStarterCard(typeof(GuZhenRenCharacter), 4)]
public sealed class GuZhenRenDefend
    : ModCardTemplate, ICardRewardExcluded
{
    protected override HashSet<CardTag> CanonicalTags =>
        [CardTag.Defend];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new BlockVar(6m, ValueProp.Move)];

    public override bool GainsBlock => true;

    public override CardAssetProfile AssetProfile =>
        global::GuZhenRen.Cards.CardImageCatalog.Create(GetType());

    // GuZhenRenCardPool is an auxiliary pool rather than the character's
    // primary pool, so CardModel.Pool cannot discover it through
    // ModelDb.AllCardPools. Return it explicitly to keep rendering and
    // multiplayer combat from failing when this starter card is inspected.
    public override CardPoolModel Pool =>
        ModelDb.CardPool<GuZhenRenCardPool>();

    public GuZhenRenDefend()
        : base(
            baseCost: 1,
            type: CardType.Skill,
            rarity: CardRarity.Basic,
            target: TargetType.Self
        )
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        ArgumentNullException.ThrowIfNull(choiceContext);
        ArgumentNullException.ThrowIfNull(cardPlay);

        await CreatureCmd.GainBlock(
            Owner.Creature,
            DynamicVars.Block,
            cardPlay
        );
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3m);
    }
}
