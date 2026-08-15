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
public sealed class FeiXiongZhuang : AbstractLiDaoCompanionCard
{
    public override Type TrainedGuType => typeof(FeiXiongZhiLiGu);

    private decimal _upDamage;
    private decimal _upBlockBonus;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(8m, ValueProp.Move),
        new DynamicVar("BlockBonus", 3m),
    ];

    public FeiXiongZhuang() : base(CardType.Attack, TargetType.AnyEnemy)
    {
        RefreshRankValues();
    }

    private static int DamageAtRank(int rank) => rank switch
    {
        <= 1 => 8, 2 => 9, 3 => 10, 4 => 11, 5 => 13,
        6 => 14, 7 => 16, 8 => 17, _ => 19,
    };

    private static int BlockBonusAtRank(int rank) => rank switch
    {
        <= 2 => 3,
        3 => 4,
        4 => 5,
        5 => 6,
        _ => 0,
    };

    private static int DivineMightAtRank(int rank) => rank switch
    {
        6 => 6,
        7 => 7,
        8 => 8,
        >= 9 => 9,
        _ => 0,
    };

    private static int QuakeAtRank(int rank) => rank switch
    {
        8 => 4,
        >= 9 => 6,
        _ => 0,
    };

    protected override void RefreshRankValues()
    {
        DynamicVars.Damage.BaseValue =
            DamageAtRank(GuRank) + _upDamage;
        DynamicVars["BlockBonus"].BaseValue =
            BlockBonusAtRank(GuRank) +
            _upBlockBonus;
    }

    protected override void AddExtraArgsToDescription(
        LocString description
    )
    {
        base.AddExtraArgsToDescription(description);
        int rank = GuRank;
        description.Add("BlockRange", rank <= 5 ? 1 : 0);
        description.Add(
            "BlockBonus",
            BlockBonusAtRank(rank)
        );
        description.Add("DivineRange", rank >= 6 ? 1 : 0);
        description.Add(
            "DivineMight",
            DivineMightAtRank(rank)
        );
        description.Add("QuakeRange", rank >= 8 ? 1 : 0);
        description.Add(
            "Quake",
            QuakeAtRank(rank)
        );
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        Creature target = cardPlay.Target!;
        int rank = GuRank;
        decimal damage = DynamicVars.Damage.BaseValue;

        if (target.Block > 0)
        {
            if (rank <= 5)
            {
                damage += DynamicVars["BlockBonus"].BaseValue;
            }
            else
            {
                await DamageCmd.Attack(damage)
                    .FromCard(this, cardPlay)
                    .Targeting(target)
                    .WithHitFx("vfx/vfx_heavy_blunt")
                    .Execute(choiceContext);

                int divine = DivineMightAtRank(rank);
                if (divine > 0 && target.IsAlive)
                {
                    await CreatureCmd.Damage(
                        choiceContext,
                        target,
                        divine,
                        ValueProp.Unblockable | ValueProp.Unpowered,
                        Owner.Creature,
                        this,
                        cardPlay
                    );
                }

                await ApplyQuakeAsync(choiceContext, rank, target);
                return;
            }
        }

        await DamageCmd.Attack(damage)
            .FromCard(this, cardPlay)
            .Targeting(target)
            .WithHitFx("vfx/vfx_heavy_blunt")
            .Execute(choiceContext);

        await ApplyQuakeAsync(choiceContext, rank, target);
    }

    private async Task ApplyQuakeAsync(
        PlayerChoiceContext choiceContext,
        int rank,
        Creature target
    )
    {
        if (rank < 8 || CombatState == null)
        {
            return;
        }

        int quake = QuakeAtRank(rank);
        foreach (Creature enemy in GuZhenRenDeterminism
                     .OrderCreatures(CombatState.HittableEnemies)
                     .Where(enemy => enemy.IsAlive &&
                         !ReferenceEquals(enemy, target)))
        {
            await CreatureCmd.Damage(
                choiceContext,
                enemy,
                quake,
                ValueProp.Unpowered,
                Owner.Creature,
                this,
                cardPlay: null
            );
        }
    }

    protected override void OnUpgrade()
    {
        _upDamage += 3m;
        _upBlockBonus += 2m;
        RefreshRankValues();
    }
}
