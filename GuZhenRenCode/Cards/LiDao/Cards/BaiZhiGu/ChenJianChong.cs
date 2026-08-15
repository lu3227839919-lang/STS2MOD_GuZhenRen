using System.Runtime.CompilerServices;

using GuZhenRen.Cards.HeLian;
using GuZhenRen.Characters;
using GuZhenRen.Multiplayer;
using GuZhenRen.Powers.LiDao;

using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;

namespace GuZhenRen.Cards.LiDao;

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class ChenJianChong : AbstractLiDaoCompanionCard
{
    public override Type TrainedGuType => typeof(BaiZhiGu);

    private decimal _upDamage;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(7m, ValueProp.Move)];

    public ChenJianChong() : base(CardType.Attack, TargetType.AnyEnemy)
    {
        RefreshRankValues();
    }

    protected override void RefreshRankValues() =>
        DynamicVars.Damage.BaseValue =
            LiDaoCompanionRankTable.ChenJianChongDamage(GuRank) + _upDamage;

    protected override void AddExtraArgsToDescription(
        LocString description
    )
    {
        base.AddExtraArgsToDescription(description);
        int rank = GuRank;
        description.Add("FirstAttackRange", rank is >= 3 and <= 6 ? 1 : 0);
        description.Add(
            "FirstAttackBonus",
            LiDaoCompanionRankTable.ChenJianChongFirstAttackBonus(rank)
        );
        description.Add("NoBlockRange", rank >= 7 ? 1 : 0);
        description.Add(
            "NoBlockBonus",
            LiDaoCompanionRankTable.ChenJianChongNoBlockBonus(rank)
        );
    }

    protected override Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        Creature target = cardPlay.Target!;
        int rank = GuRank;
        decimal damage = DynamicVars.Damage.BaseValue;

        if (!PlayedAttackEarlierThisTurn() &&
            rank is >= 3 and <= 6)
        {
            damage += LiDaoCompanionRankTable
                .ChenJianChongFirstAttackBonus(rank);
        }
        if (target.Block <= 0 && rank >= 7)
        {
            damage += LiDaoCompanionRankTable
                .ChenJianChongNoBlockBonus(rank);
        }

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
