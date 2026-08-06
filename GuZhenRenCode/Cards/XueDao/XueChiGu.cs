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
public sealed class XueChiGu : AbstractGuWormCard
{
    private const string OverflowBlockVar = "OverflowBlock";

    public override int MaxUses => 1;

    public override int RecoveryDelayTurns => GuRank >= 6 ? 3 : 2;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<XueYuanPower>(2m),
        new DynamicVar(OverflowBlockVar, 3m),
    ];

    public XueChiGu()
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
        int requested = DynamicVars[typeof(XueYuanPower).Name].IntValue;
        int gained = await XueDaoPowerSystem.GainXueYuan(
            choiceContext,
            this,
            requested
        );
        int overflow = Math.Max(0, requested - gained);

        if (overflow > 0)
        {
            await CreatureCmd.GainBlock(
                Owner.Creature,
                overflow * DynamicVars[OverflowBlockVar].BaseValue,
                ValueProp.Unpowered | ValueProp.Move,
                cardPlay
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
        DynamicVars[typeof(XueYuanPower).Name].BaseValue = GuRank switch
        {
            <= 2 => 2,
            <= 4 => 3,
            <= 6 => 4,
            <= 8 => 5,
            _ => 6,
        };
        DynamicVars[OverflowBlockVar].BaseValue = GuRank switch
        {
            <= 3 => 3,
            <= 6 => 4,
            <= 8 => 5,
            _ => 6,
        };
    }
}
