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

public abstract class AbstractYueMangToken
    : AbstractGuZhenRenCard,
      ICardRewardExcluded
{
    public override CardPoolModel Pool =>
        ModelDb.CardPool<GuZhenRenCardPool>();

    public override bool CanBeGeneratedInCombat => false;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Exhaust];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/YueMangGu.png"
    );

    protected AbstractYueMangToken(int cost)
        : base(
            cost,
            CardType.Attack,
            CardRarity.Token,
            TargetType.AnyEnemy
        )
    {
        SetDao(Dao.GuangDao);
    }

    protected async Task AttackMany(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        Creature target,
        int hitCount,
        decimal damage,
        decimal finalHitBonus = 0
    )
    {
        for (int hit = 0; hit < hitCount; hit++)
        {
            decimal currentDamage = damage;
            if (hit == hitCount - 1)
            {
                currentDamage += finalHitBonus;
            }

            await DamageCmd
                .Attack(currentDamage)
                .FromCard(this, cardPlay)
                .Targeting(target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);

            if (target.IsDead)
            {
                break;
            }
        }
    }
}

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class YueMang : AbstractYueMangToken
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(4m, ValueProp.Move),
        new RepeatVar("Hits", 2),
        new DynamicVar("RefractionBonus", 4m),
    ];

    public YueMang() : base(1)
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

        bool afterSkill = Owner.Creature.GetPower<ZheGuangPower>()?
            .PreviousCardWas(CardType.Skill) == true;
        await AttackMany(
            choiceContext,
            cardPlay,
            target,
            DynamicVars["Hits"].IntValue,
            DynamicVars.Damage.BaseValue,
            afterSkill
                ? DynamicVars["RefractionBonus"].BaseValue
                : 0
        );
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(1m);
        DynamicVars["RefractionBonus"].UpgradeValueBy(2m);
    }
}

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class NingYueMang : AbstractYueMangToken
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(6m, ValueProp.Move),
        new RepeatVar("Hits", 2),
    ];

    public NingYueMang() : base(1)
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

        bool afterSkill = Owner.Creature.GetPower<ZheGuangPower>()?
            .PreviousCardWas(CardType.Skill) == true;
        bool hadZhaoPo = target.GetPower<ZhaoPoPower>() is { Amount: > 0 };

        await AttackMany(
            choiceContext,
            cardPlay,
            target,
            DynamicVars["Hits"].IntValue + (afterSkill ? 1 : 0),
            DynamicVars.Damage.BaseValue
        );

        if (GuRank >= 7 && hadZhaoPo && cardPlay.PlayIndex == 0)
        {
            CanMang remnant = GuGeneratedCardFactory.Create<CanMang>(
                Owner,
                GuRank
            );
            await GuGeneratedCardFactory.AddToHandOrDiscard(
                remnant,
                Owner
            );
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
    }
}

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class CanMang : AbstractYueMangToken
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(7m, ValueProp.Move),
        new DynamicVar("RefractionDamage", 10m),
    ];

    public CanMang() : base(0)
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

        bool refracted = Owner.Creature.GetPower<ZheGuangPower>()?
            .GuangHuiGainedThisTurn > 0;
        decimal damage = refracted
            ? DynamicVars["RefractionDamage"].BaseValue
            : DynamicVars.Damage.BaseValue;

        await DamageCmd
            .Attack(damage)
            .FromCard(this, cardPlay)
            .Targeting(target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
        DynamicVars["RefractionDamage"].UpgradeValueBy(2m);
    }
}

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class TianYueMang : AbstractYueMangToken
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(8m, ValueProp.Move),
        new RepeatVar("Hits", 4),
        new DynamicVar("DoubleRefractionBonus", 12m),
        new PowerVar<ZhaoPoPower>(2m),
    ];

    public TianYueMang() : base(2)
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

        bool doubleRefraction = Owner.Creature.GetPower<ZheGuangPower>()?
            .GuangHuiGainedThisTurn >= 2;
        await AttackMany(
            choiceContext,
            cardPlay,
            target,
            DynamicVars["Hits"].IntValue,
            DynamicVars.Damage.BaseValue
        );

        if (doubleRefraction && !target.IsDead)
        {
            await DamageCmd
                .Attack(DynamicVars["DoubleRefractionBonus"].BaseValue)
                .FromCard(this, cardPlay)
                .Targeting(target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);

            await GuangDaoPowerSystem.ApplyZhaoPo(
                choiceContext,
                this,
                target,
                DynamicVars[typeof(ZhaoPoPower).Name].IntValue
            );
        }
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
        DynamicVars.Damage.UpgradeValueBy(1m);
    }
}
