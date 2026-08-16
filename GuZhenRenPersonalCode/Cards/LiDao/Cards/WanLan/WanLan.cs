using GuZhenRen.Characters;
using GuZhenRen.Combat;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;

using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;

namespace GuZhenRen.Cards.LiDao;

/// <summary>
/// 挽澜：力道多段攻击续航蛊，无需解封。
/// 对一名敌人造成多段伤害；每有 2 次攻击伤及其生命回复 1 点能量；
/// 7转起若全部攻击均伤及其生命，额外回复 1 点能量。
/// 格挡住的段数不计数。
/// </summary>
[RegisterCard(typeof(GuZhenRenGuCardPool))]
public sealed class WanLan : AbstractGuWormCard
{
    public override int MinimumAvailableGuRank => 5;

    public override int MaxGuRank => 7;

    public override int RecoveryDelayTurns => GuRank switch
    {
        <= 5 => 2,
        6 => 3,
        _ => 3,
    };

    public WanLan()
        : base(
            0,
            CardType.Attack,
            CardRarity.Uncommon,
            TargetType.AnyEnemy
        )
    {
        SetDao(Dao.LiDao);
        this.SecondaryCosts().Set(YuanQiSystem.ResourceId, 1);
    }

    protected override void AddExtraArgsToDescription(
        LocString description
    )
    {
        base.AddExtraArgsToDescription(description);
        description.Add("Damage", DamageAtRank(GuRank));
        description.Add("Hits", HitsAtRank(GuRank));
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        Creature? target = cardPlay.Target;
        if (target == null)
        {
            return;
        }

        int hits = HitsAtRank(GuRank);
        int damage = DamageAtRank(GuRank);

        AttackCommand attack = DamageCmd
            .Attack(damage)
            .WithHitCount(hits)
            .FromCard(this, cardPlay)
            .Targeting(target)
            .WithHitFx("vfx/vfx_attack_blunt");
        await attack.Execute(choiceContext);

        int hpHits = attack.Results
            .SelectMany(hit => hit)
            .Count(result => result.UnblockedDamage > 0);

        int energy = hpHits / 2;
        if (GuRank >= 7 && hpHits == hits)
        {
            energy += 1;
        }

        if (energy > 0)
        {
            await PlayerCmd.GainEnergy(energy, Owner);
        }
    }

    internal static int DamageAtRank(int rank) => 4;

    internal static int HitsAtRank(int rank) => rank switch
    {
        >= 7 => 5,
        6 => 4,
        _ => 3,
    };
}
