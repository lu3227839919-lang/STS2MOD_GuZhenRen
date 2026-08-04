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

public abstract class AbstractLightExpansionToken
    : AbstractGuZhenRenCard,
      ICardRewardExcluded
{
    public override CardPoolModel Pool =>
        ModelDb.CardPool<GuZhenRenCardPool>();

    public override bool CanBeGeneratedInCombat => false;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Exhaust];

    protected AbstractLightExpansionToken(
        int baseCost,
        CardType type,
        TargetType target
    )
        : base(
            baseCost,
            type,
            CardRarity.Token,
            target
        )
    {
        SetDao(Dao.GuangDao);
    }
}

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

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class FanZhao : AbstractLightExpansionToken
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("MaxDamage", 15m)];

    public override CardAssetProfile AssetProfile =>
        global::GuZhenRen.Cards.CardImageCatalog.Create(GetType());

    public FanZhao() : base(1, CardType.Attack, TargetType.AnyEnemy)
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

        decimal damage = Math.Min(
            DynamicVars["MaxDamage"].BaseValue,
            Owner.Creature.Block / 2m
        );

        await DamageCmd
            .Attack(damage)
            .FromCard(this, cardPlay)
            .Targeting(target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["MaxDamage"].UpgradeValueBy(5m);
    }
}

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class MingJing : AbstractLightExpansionToken
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new BlockVar(20m, ValueProp.Move)];

    public override bool GainsBlock => true;

    public override CardAssetProfile AssetProfile =>
        global::GuZhenRen.Cards.CardImageCatalog.Create(GetType());

    public MingJing() : base(2, CardType.Skill, TargetType.Self)
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
        await GuangDaoPowerSystem.GainGuangHui(
            choiceContext,
            this,
            2
        );
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
        DynamicVars.Block.UpgradeValueBy(4m);
    }
}

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class DingGuangFu : AbstractLightExpansionToken
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("BonusDamage", 5m)];

    public override CardAssetProfile AssetProfile =>
        global::GuZhenRen.Cards.CardImageCatalog.Create(GetType());

    public DingGuangFu() : base(0, CardType.Skill, TargetType.Self)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        await PowerCmd.Apply<DingGuangChargePower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["BonusDamage"].IntValue,
            Owner.Creature,
            this
        );

        if (GuRank >= 7 && cardPlay.PlayIndex == 0)
        {
            GuangBiao marker = GuGeneratedCardFactory.Create<GuangBiao>(
                Owner,
                GuRank
            );
            await GuGeneratedCardFactory.AddToHandOrDiscard(
                marker,
                Owner
            );
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["BonusDamage"].UpgradeValueBy(3m);
    }
}

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class GuangBiao : AbstractLightExpansionToken
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("LightBonus", 4m)];

    public override CardAssetProfile AssetProfile =>
        global::GuZhenRen.Cards.CardImageCatalog.Create(GetType());

    public GuangBiao() : base(0, CardType.Skill, TargetType.Self)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
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
public sealed class RiYun : AbstractLightExpansionToken
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("TotalZhaoPo", 6m),
        new DynamicVar("TargetCap", 4m),
    ];

    public override CardAssetProfile AssetProfile =>
        global::GuZhenRen.Cards.CardImageCatalog.Create(GetType());

    public RiYun() : base(2, CardType.Skill, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        Creature? primary = cardPlay.Target;
        if (primary == null || !IsValidTarget(primary) ||
            Owner.Creature.CombatState is not { } combatState)
        {
            return;
        }

        Creature[] targets = GuZhenRenDeterminism
            .OrderCreatures(combatState.HittableEnemies)
            .Where(enemy => !enemy.IsDead)
            .ToArray();

        int remaining = DynamicVars["TotalZhaoPo"].IntValue;
        int primaryAmount = Math.Min(
            remaining,
            DynamicVars["TargetCap"].IntValue
        );
        await GuangDaoPowerSystem.ApplyZhaoPo(
            choiceContext,
            this,
            primary,
            primaryAmount
        );
        remaining -= primaryAmount;

        Creature[] secondaryTargets = targets
            .Where(enemy => !ReferenceEquals(enemy, primary))
            .ToArray();

        // 剩余额度按确定性顺序轮流分配；只有一个目标时，
        // 受单体上限约束而未使用的额度会自然舍弃。
        for (int index = 0; remaining > 0 && secondaryTargets.Length > 0; index++)
        {
            Creature enemy = secondaryTargets[index % secondaryTargets.Length];
            await GuangDaoPowerSystem.ApplyZhaoPo(
                choiceContext,
                this,
                enemy,
                1
            );
            remaining--;
        }
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
        DynamicVars["TotalZhaoPo"].UpgradeValueBy(2m);
    }
}
