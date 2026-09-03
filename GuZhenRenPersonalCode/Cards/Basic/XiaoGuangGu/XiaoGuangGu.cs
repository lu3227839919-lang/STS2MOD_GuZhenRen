using GuZhenRen.Characters;
using GuZhenRen.Combat;
using GuZhenRen.Powers.GuangDao;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.GuangDao;

[RegisterCard(typeof(GuZhenRenGuCardPool))]
[RegisterCharacterStarterCard(typeof(GuZhenRenCharacter), 2)]
public sealed class XiaoGuangGu
    : AbstractGuWormCard,
      IRefractionRelevantCard,
      IJuGuangCard
{
    public override int MinimumAvailableGuRank => 1;

    public override int MaxGuRank => 2;

    public override int MaxUses => 1;

    public override int RecoveryDelayTurns => 1;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("JuGuang", 1),
        new DynamicVar("RefractionJuGuang", 1),
    ];

    public override CardAssetProfile AssetProfile =>
        CardImageCatalog.Create(GetType());

    public XiaoGuangGu()
        : base(0, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        SetDao(Dao.GuangDao);
        this.SecondaryCosts().Set(YuanQiSystem.ResourceId, 1);
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        RefractionResult refraction = GuRank >= 2
            ? await GuangDaoPowerSystem.ResolveRefractionEffectAsync(
                choiceContext,
                this,
                cardPlay
            )
            : GuangDaoPowerSystem.GetRefractionResult(this, cardPlay);

        int amount = DynamicVars["JuGuang"].IntValue;
        if (GuRank >= 2)
        {
            amount += DynamicVars["RefractionJuGuang"].IntValue *
                refraction.EffectResolutionCount;
        }

        await PowerCmd.Apply<JuGuangPower>(
            choiceContext,
            Owner.Creature,
            amount,
            Owner.Creature,
            this
        );
    }
}
