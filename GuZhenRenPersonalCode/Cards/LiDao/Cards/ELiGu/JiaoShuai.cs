using GuZhenRen.Characters;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;

namespace GuZhenRen.Cards.LiDao;

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class JiaoShuai : AbstractLiDaoCompanionCard
{
    public override Type SourceGuType => typeof(ELiGu);

    private decimal _upDamage;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(4m, ValueProp.Move),
        new DynamicVar("Hits", 2m),
        new DynamicVar("SecondHitBonus", 0m),
    ];

    public JiaoShuai() : base(CardType.Attack, TargetType.AnyEnemy) =>
        RefreshRankValues();

    private static int DamageAtRank(int rank) => rank switch
    {
        <= 2 => 4,
        3 or 4 => 5,
        _ => 6,
    };

    private static int SecondHitBonusAtRank(int rank) =>
        rank >= 4 ? 2 : 0;

    protected override void RefreshRankValues()
    {
        DynamicVars.Damage.BaseValue = DamageAtRank(GuRank) + _upDamage;
        DynamicVars["Hits"].BaseValue = 2m;
        DynamicVars["SecondHitBonus"].BaseValue =
            SecondHitBonusAtRank(GuRank);
    }

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        description.Add("SecondHitRange", GuRank >= 4 ? 1 : 0);
        description.Add(
            "SecondHitBonus",
            SecondHitBonusAtRank(GuRank)
        );
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        Creature target = cardPlay.Target!;
        for (int hit = 0; hit < 2 && target.IsAlive; hit++)
        {
            decimal damage = DynamicVars.Damage.BaseValue;
            if (hit == 1)
            {
                damage += SecondHitBonusAtRank(GuRank);
            }

            await DamageCmd.Attack(damage)
                .FromCard(this, cardPlay)
                .Targeting(target)
                .WithHitFx("vfx/vfx_attack_blunt")
                .Execute(choiceContext);
        }
    }

    protected override void OnUpgrade()
    {
        _upDamage += 1m;
        RefreshRankValues();
    }
}
