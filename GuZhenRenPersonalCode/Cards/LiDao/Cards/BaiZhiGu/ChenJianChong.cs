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
public sealed class ChenJianChong : AbstractLiDaoCompanionCard
{
    public override Type SourceGuType => typeof(BaiZhiGu);

    private decimal _upDamage;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(8m, ValueProp.Move)];

    public ChenJianChong() : base(CardType.Attack, TargetType.AnyEnemy) =>
        RefreshRankValues();

    private static int DamageAtRank(int rank) => rank switch
    {
        <= 2 => 8,
        3 => 9,
        4 => 10,
        _ => 11,
    };

    private static int FirstAttackBonusAtRank(int rank) => rank switch
    {
        4 => 2,
        >= 5 => 3,
        _ => 0,
    };

    protected override void RefreshRankValues() =>
        DynamicVars.Damage.BaseValue = DamageAtRank(GuRank) + _upDamage;

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        description.Add("FirstAttackRange", GuRank >= 4 ? 1 : 0);
        description.Add(
            "FirstAttackBonus",
            FirstAttackBonusAtRank(GuRank)
        );
    }

    protected override Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        decimal damage = DynamicVars.Damage.BaseValue;
        if (GuRank >= 4 && !PlayedAttackEarlierThisTurn())
        {
            damage += FirstAttackBonusAtRank(GuRank);
        }

        Creature target = cardPlay.Target!;
        return DamageCmd.Attack(damage)
            .FromCard(this, cardPlay)
            .Targeting(target)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        _upDamage += 3m;
        RefreshRankValues();
    }
}
