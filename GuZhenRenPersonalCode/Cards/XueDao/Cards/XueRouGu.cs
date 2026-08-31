using GuZhenRen.Characters;
using GuZhenRen.Combat;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;

namespace GuZhenRen.Cards.XueDao;

[RegisterCard(typeof(GuZhenRenGuCardPool))]
public sealed class XueRouGu : AbstractGuWormCard
{
    public override int MaxGuRank => 5;

    public override int MaxUses => 1;

    public override int RecoveryDelayTurns => 2;

    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new BlockVar(4m, ValueProp.Move)];

    public XueRouGu()
        : base(0, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        SetDao(Dao.XueDao);
        this.SecondaryCosts().Set(YuanQiSystem.ResourceId, YuanQiCost);
        RefreshRankValues();
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
    }

    protected override void OnGuRankChanged()
    {
        base.OnGuRankChanged();
        RefreshRankValues();
    }

    private void RefreshRankValues()
    {
        DynamicVars.Block.BaseValue =
            4 + (Math.Clamp(GuRank, 1, 5) - 1) * 2;
    }
}
