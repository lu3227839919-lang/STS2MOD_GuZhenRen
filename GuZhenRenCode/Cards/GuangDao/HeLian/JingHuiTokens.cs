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

public abstract class AbstractJingHuiToken
    : AbstractGuZhenRenCard,
      ICardRewardExcluded
{
    public override CardPoolModel Pool =>
        ModelDb.CardPool<GuZhenRenCardPool>();

    public override bool CanBeGeneratedInCombat => false;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Exhaust];

    public override CardAssetProfile AssetProfile =>
        global::GuZhenRen.Cards.CardImageCatalog.Create(GetType());

    protected AbstractJingHuiToken(
        int cost,
        TargetType target = TargetType.Self
    )
        : base(
            cost,
            CardType.Skill,
            CardRarity.Token,
            target
        )
    {
        SetDao(Dao.GuangDao);
    }
}

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class JingHui : AbstractJingHuiToken
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(9m, ValueProp.Move),
        new DynamicVar("ZhaoPoBonus", 5m),
    ];

    public override bool GainsBlock => true;

    public JingHui() : base(1)
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
        await PowerCmd.Apply<DingGuangChargePower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["ZhaoPoBonus"].IntValue,
            Owner.Creature,
            this
        );
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3m);
        DynamicVars["ZhaoPoBonus"].UpgradeValueBy(2m);
    }
}

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

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class FanHui : AbstractJingHuiToken
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("LightBonus", 4m)];

    public FanHui() : base(0)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        await GuangDaoPowerSystem.GainGuangHui(
            choiceContext,
            this,
            1
        );
        await PowerCmd.Apply<JuGuangPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["LightBonus"].IntValue,
            Owner.Creature,
            this
        );
    }

    protected override void OnUpgrade()
    {
        DynamicVars["LightBonus"].UpgradeValueBy(2m);
    }
}

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class ZhouTianJingHui : AbstractJingHuiToken
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(20m, ValueProp.Move),
        new PowerVar<ZhaoPoPower>(3m),
        new DynamicVar("LightBonus", 8m),
    ];

    public override bool GainsBlock => true;

    public ZhouTianJingHui()
        : base(2, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        Creature? target = cardPlay.Target;
        if (target == null || !IsValidTarget(target))
        {
            return;
        }

        await CreatureCmd.GainBlock(
            Owner.Creature,
            DynamicVars.Block,
            cardPlay
        );
        await GuangDaoPowerSystem.ApplyZhaoPo(
            choiceContext,
            this,
            target,
            DynamicVars[typeof(ZhaoPoPower).Name].IntValue
        );
        await PowerCmd.Apply<JuGuangPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["LightBonus"].IntValue,
            Owner.Creature,
            this
        );
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
        DynamicVars.Block.UpgradeValueBy(4m);
    }
}
