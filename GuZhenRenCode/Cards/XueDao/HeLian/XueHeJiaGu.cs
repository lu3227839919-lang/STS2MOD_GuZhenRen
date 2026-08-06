using GuZhenRen.Cards.XueDao;
using GuZhenRen.Combat;
using GuZhenRen.Powers.XueDao;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.HeLian;

[HeLianRecipe(
    typeof(XueChiGu),
    typeof(XuePiGu),
    MinimumMaterialRank = 3
)]
public sealed class XueHeJiaGu : AbstractHeLianGuCard
{
    private const string LowBlockVar = "LowBlock";
    private const string HighBlockVar = "HighBlock";
    private const string LowBloodGainVar = "LowBloodGain";

    public override int MaxUses => 1;

    public override int RecoveryDelayTurns => 3;

    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar(LowBlockVar, 14m),
        new DynamicVar(HighBlockVar, 24m),
        new DynamicVar(LowBloodGainVar, 3m),
    ];

    public XueHeJiaGu()
        : base(0, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
        SetDao(Dao.XueDao);
        this.SecondaryCosts().Set(YuanQiSystem.ResourceId, 2);
        RefreshRankValues();
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        int skull = XueDaoPowerSystem.GetXueLu(Owner.Creature);
        bool highTier = XueDaoPowerSystem.GetXueYuan(Owner.Creature) >= 4;

        if (!highTier)
        {
            await XueDaoPowerSystem.GainXueYuan(
                choiceContext,
                this,
                DynamicVars[LowBloodGainVar].IntValue
            );

            await CreatureCmd.GainBlock(
                Owner.Creature,
                DynamicVars[LowBlockVar].BaseValue + skull * 2,
                ValueProp.Unpowered | ValueProp.Move,
                cardPlay
            );
            return;
        }

        if (!await XueDaoPowerSystem.TrySpendXueYuan(
                choiceContext,
                this,
                2
            ))
        {
            return;
        }

        int block = DynamicVars[HighBlockVar].IntValue + skull * 2;
        await CreatureCmd.GainBlock(
            Owner.Creature,
            block,
            ValueProp.Unpowered | ValueProp.Move,
            cardPlay
        );

        await PowerCmd.Apply<XueHeJiaCarryPower>(
            choiceContext,
            Owner.Creature,
            Math.Max(1, block / 2),
            Owner.Creature,
            this
        );
    }

    protected override int CalculateHeLianResultRank(
        IReadOnlyList<CardModel> materials
    ) => Math.Min(
        MaxGuRank,
        materials
            .OfType<IGuRankProvider>()
            .Select(provider => provider.GuRank)
            .DefaultIfEmpty(1)
            .Min() + 1
    );

    protected override void OnGuRankChanged()
    {
        base.OnGuRankChanged();
        RefreshRankValues();
    }

    private void RefreshRankValues()
    {
        DynamicVars[LowBlockVar].BaseValue = 10 + GuRank * 2;
        DynamicVars[HighBlockVar].BaseValue = 18 + GuRank * 4;
        DynamicVars[LowBloodGainVar].BaseValue = GuRank >= 7 ? 4 : 3;
    }
}
