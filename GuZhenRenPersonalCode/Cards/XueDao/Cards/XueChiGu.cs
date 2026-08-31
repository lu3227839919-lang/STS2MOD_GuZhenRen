using GuZhenRen.Characters;
using GuZhenRen.Combat;
using GuZhenRen.Powers.XueDao;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;

using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;

namespace GuZhenRen.Cards.XueDao;

[RegisterCard(typeof(GuZhenRenGuCardPool))]
public sealed class XueChiGu : AbstractGuWormCard
{
    public override int MinimumAvailableGuRank => 3;

    public override int MaxGuRank => 6;

    public override int MaxUses => 1;

    public override int RecoveryDelayTurns => GuRank <= 4 ? 3 : 2;

    public XueChiGu()
        : base(0, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
        SetDao(Dao.XueDao);
        SetGuRank(MinimumAvailableGuRank);
        this.SecondaryCosts().Set(YuanQiSystem.ResourceId, YuanQiCost);
    }

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        description.Add("MaxRefine", GuRank >= 5 ? 2 : 1);
        description.Add("EnergyGain", 1);
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        await PlayerCmd.GainEnergy(1, Owner);
        int consumed = await XueDaoCardSystem.ConsumeSelectedRemains(
            choiceContext,
            Owner,
            GuRank >= 5 ? 2 : 1
        );

        if (consumed > 0)
        {
            await PowerCmd.Apply<XueChiPower>(
                choiceContext,
                Owner.Creature,
                consumed,
                Owner.Creature,
                this
            );
        }
    }
}
