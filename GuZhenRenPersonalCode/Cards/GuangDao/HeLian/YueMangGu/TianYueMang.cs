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

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        base.CanonicalKeywords
            .Append(global::GuZhenRen.Cards.GuZhenRenKeywords.ZheGuangCore)
            .Distinct();

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
