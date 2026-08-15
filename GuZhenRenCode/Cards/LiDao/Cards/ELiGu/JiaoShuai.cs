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
public sealed class JiaoShuai : AbstractLiDaoCompanionCard
{
    public override Type TrainedGuType => typeof(ELiGu);

    private decimal _upDamage;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(4m, ValueProp.Move),
        new DynamicVar("Hits", 2m),
    ];

    public JiaoShuai() : base(CardType.Attack, TargetType.AnyEnemy)
    {
        RefreshRankValues();
    }

    protected override void RefreshRankValues()
    {
        DynamicVars.Damage.BaseValue =
            LiDaoCompanionRankTable.JiaoShuaiDamage(GuRank) + _upDamage;
        DynamicVars["Hits"].BaseValue =
            LiDaoCompanionRankTable.JiaoShuaiHits(GuRank);
    }

    protected override void AddExtraArgsToDescription(
        LocString description
    )
    {
        base.AddExtraArgsToDescription(description);
        int rank = GuRank;
        description.Add("LastHitRange", rank is >= 3 and <= 4 ? 1 : 0);
        description.Add(
            "LastHitBonus",
            LiDaoCompanionRankTable.JiaoShuaiLastHitBonus(rank)
        );
        description.Add("PursuitRange", rank >= 7 ? 1 : 0);
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        int rank = GuRank;
        int hits = DynamicVars["Hits"].IntValue;
        decimal baseDamage = DynamicVars.Damage.BaseValue;
        int lastHitBonus = LiDaoCompanionRankTable.JiaoShuaiLastHitBonus(rank);
        bool pursues = LiDaoCompanionRankTable.JiaoShuaiPursues(rank);

        Creature? current = cardPlay.Target;
        for (int hit = 0; hit < hits; hit++)
        {
            if (current == null || current.IsDead)
            {
                if (!pursues)
                {
                    break;
                }
                current = LiDaoPhantomSystem.FindPursuitTarget(this);
                if (current == null)
                {
                    break;
                }
            }

            decimal damage = baseDamage;
            if (lastHitBonus > 0 && hit == hits - 1)
            {
                damage += lastHitBonus;
            }

            await DamageCmd.Attack(damage)
                .FromCard(this, cardPlay)
                .Targeting(current)
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
