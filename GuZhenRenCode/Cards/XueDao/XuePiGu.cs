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
public sealed class XuePiGu : AbstractGuWormCard
{
    private const string EmpoweredBlockVar = "EmpoweredBlock";

    public override int MaxUses => IsUpgraded ? 2 : 1;

    public override int RecoveryDelayTurns => GuRank >= 6 ? 3 : 2;

    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(8m, ValueProp.Move),
        new DynamicVar(EmpoweredBlockVar, 5m),
    ];

    public XuePiGu()
        : base(0, CardType.Skill, CardRarity.Common, TargetType.Self)
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
        decimal block = DynamicVars.Block.BaseValue +
            XueDaoPowerSystem.GetXueLu(Owner.Creature) * 2;

        if (XueDaoPowerSystem.GetXueYuan(Owner.Creature) > 0 &&
            await XueDaoPowerSystem.TrySpendXueYuan(
                choiceContext,
                this,
                1
            ))
        {
            block += DynamicVars[EmpoweredBlockVar].BaseValue;
        }

        await CreatureCmd.GainBlock(
            Owner.Creature,
            block,
            ValueProp.Unpowered | ValueProp.Move,
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
        DynamicVars.Block.BaseValue = GuRank switch
        {
            <= 1 => 8,
            2 => 10,
            3 => 12,
            4 => 14,
            5 => 17,
            6 => 20,
            7 => 23,
            8 => 27,
            _ => 31,
        };
        DynamicVars[EmpoweredBlockVar].BaseValue = GuRank switch
        {
            <= 2 => 5,
            3 => 6,
            4 => 7,
            5 => 8,
            6 => 10,
            7 => 12,
            8 => 14,
            _ => 16,
        };
    }
}
