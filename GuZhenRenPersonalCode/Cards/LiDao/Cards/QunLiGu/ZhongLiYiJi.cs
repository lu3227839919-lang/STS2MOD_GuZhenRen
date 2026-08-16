using GuZhenRen.Characters;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;

namespace GuZhenRen.Cards.LiDao;

/// <summary>群力蛊的战斗内伴生牌。</summary>
[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class ZhongLiYiJi : AbstractLiDaoCompanionCard
{
    public override Type TrainedGuType => typeof(QunLiGu);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(6m, ValueProp.Move)];

    public ZhongLiYiJi() : base(CardType.Attack, TargetType.AnyEnemy)
    {
        RefreshRankValues();
    }

    protected override void RefreshRankValues() =>
        DynamicVars.Damage.BaseValue = DamageAtRank(GuRank);

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

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(target)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3m);
    }

    private static int DamageAtRank(int rank) => rank switch
    {
        <= 1 => 6,
        2 => 7,
        3 => 8,
        4 => 9,
        5 => 10,
        6 => 12,
        7 => 14,
        8 => 16,
        _ => 18,
    };
}
