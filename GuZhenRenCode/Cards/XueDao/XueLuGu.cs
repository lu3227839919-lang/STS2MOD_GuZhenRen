using GuZhenRen.Characters;
using GuZhenRen.Combat;
using GuZhenRen.Powers.XueDao;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.XueDao;

[RegisterCard(typeof(GuZhenRenGuCardPool))]
public sealed class XueLuGu : AbstractGuWormCard
{
    private const string MaxAbsorbVar = "MaxAbsorb";
    private const int OverflowHealPerRemains = 3;

    public override int MaxUses => IsUpgraded ? 2 : 1;

    public override int RecoveryDelayTurns => GuRank >= 7 ? 4 : 3;

    public override bool GainsBlock => true;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        base.CanonicalKeywords
            .Append(
                GuZhenRenKeywords.GetShiHaiKeyword(
                    GetMaximumAbsorb()
                )
            )
            .Distinct();

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(6m, ValueProp.Move),
        new DynamicVar(MaxAbsorbVar, 1m),
        new HealVar(OverflowHealPerRemains),
    ];

    public XueLuGu()
        : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        SetDao(Dao.XueDao);
        this.SecondaryCosts().Set(YuanQiSystem.ResourceId, 1);
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

        int consumed = await XueDaoCardSystem.ConsumeOldestRemains(
            choiceContext,
            Owner,
            DynamicVars[MaxAbsorbVar].IntValue
        );

        if (consumed <= 0)
        {
            return;
        }

        (_, int overflow) = await XueDaoPowerSystem.GainXueLuOrOverflow(
            choiceContext,
            this,
            consumed
        );

        if (overflow > 0)
        {
            await CreatureCmd.Heal(
                Owner.Creature,
                overflow * OverflowHealPerRemains
            );
        }
    }

    protected override void OnGuRankChanged()
    {
        base.OnGuRankChanged();
        RefreshRankValues();
    }

    private void RefreshRankValues()
    {
        DynamicVars.Block.BaseValue = GuRank switch
        {
            <= 1 => 6,
            2 => 7,
            3 => 9,
            4 => 11,
            5 => 13,
            6 => 15,
            7 => 18,
            8 => 21,
            _ => 24,
        };
        DynamicVars[MaxAbsorbVar].BaseValue = GetMaximumAbsorb();
    }

    private int GetMaximumAbsorb() =>
        GuRank switch
        {
            <= 3 => 1,
            <= 6 => 2,
            _ => 3,
        };
}
