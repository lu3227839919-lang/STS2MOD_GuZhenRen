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

    private static int DamageAtRank(int rank) => rank switch
    {
        <= 1 => 7, 2 => 8, 3 => 9, 4 => 10, 5 => 11,
        6 => 13, 7 => 14, 8 => 16, _ => 18,
    };

    private static int FirstAttackBonusAtRank(int rank) => rank switch
    {
        3 or 4 => 2,
        5 or 6 => 3,
        _ => 0,
    };

    private static int NoBlockBonusAtRank(int rank) => rank switch
    {
        7 => 3,
        8 => 4,
        >= 9 => 5,
        _ => 0,
    };

    protected override void RefreshRankValues() =>
        DynamicVars.Damage.BaseValue =
            DamageAtRank(GuRank) + _upDamage;

    protected override void AddExtraArgsToDescription(
        LocString description
    )
    {
        base.AddExtraArgsToDescription(description);
        int rank = GuRank;
        description.Add("FirstAttackRange", rank is >= 3 and <= 6 ? 1 : 0);
        description.Add(
            "FirstAttackBonus",
            FirstAttackBonusAtRank(rank)
        );
        description.Add("NoBlockRange", rank >= 7 ? 1 : 0);
        description.Add(
            "NoBlockBonus",
            NoBlockBonusAtRank(rank)
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
            damage += FirstAttackBonusAtRank(rank);
        }
        if (target.Block <= 0 && rank >= 7)
        {
            damage += NoBlockBonusAtRank(rank);
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
