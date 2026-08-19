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

namespace GuZhenRen.Cards.GuangDao;

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class LiuGuang : AbstractLiuGuangToken
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(7m, ValueProp.Move),
        new DynamicVar("RefractionBonus", 4m),
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        base.CanonicalKeywords
            .Append(GuZhenRenKeywords.ZheGuangCore)
            .Concat(
                GuRank >= 7
                    ? [GuZhenRenKeywords.ZhaoXi]
                    : []
            )
            .Distinct();

    public override CardAssetProfile AssetProfile =>
        CardImageCatalog.Create(GetType());

    public LiuGuang() : base(1)
    {
    }

    protected override void AddExtraArgsToDescription(
        MegaCrit.Sts2.Core.Localization.LocString description
    )
    {
        base.AddExtraArgsToDescription(description);
        description.Add(
            "AfterSkillDamage",
            DynamicVars.Damage.IntValue +
                DynamicVars["RefractionBonus"].IntValue
        );
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
        decimal damage = DynamicVars.Damage.BaseValue +
            (afterSkill
                ? DynamicVars["RefractionBonus"].BaseValue
                : 0);

        await DamageCmd
            .Attack(damage)
            .FromCard(this, cardPlay)
            .Targeting(target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        if (GuRank >= 7 && hadZhaoPo && cardPlay.PlayIndex == 0)
        {
            LiuHui afterglow = GuGeneratedCardFactory.Create<LiuHui>(
                Owner,
                GuRank
            );
            await GuGeneratedCardFactory.AddToHandOrDiscard(
                afterglow,
                Owner
            );
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3m);
        DynamicVars["RefractionBonus"].UpgradeValueBy(2m);
    }
}
