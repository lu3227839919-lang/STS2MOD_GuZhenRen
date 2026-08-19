using GuZhenRen.Cards.HeLian;
using GuZhenRen.Characters;
using GuZhenRen.Combat;
using GuZhenRen.Powers.ZhouDao;

using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;

namespace GuZhenRen.Cards.ZhouDao;

[RegisterCard(typeof(GuZhenRenGuCardPool))]
public sealed class HuanBuGu : AbstractZhouDaoGuCard
{
    public override int MinimumAvailableGuRank => 3;

    public override int RecoveryDelayTurns => GuRank switch
    {
        <= 5 => 2,
        <= 8 => 3,
        _ => 4,
    };

    public HuanBuGu()
        : base(CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    protected override void AddExtraArgsToDescription(
        LocString description
    )
    {
        base.AddExtraArgsToDescription(description);

        (int reduction, int duration) = GuRank switch
        {
            3 => (4, 2),
            4 => (5, 2),
            5 => (6, 2),
            6 => (7, 2),
            7 => (8, 2),
            8 => (10, 2),
            _ => (12, 3),
        };

        description.Add("Reduction", reduction);
        description.Add("Duration", duration);
        description.Add("RepeatGain", GuRank >= 7 ? 2 : 1);
        description.Add("HasRepeatGain", GuRank >= 5 ? 1 : 0);
        description.Add("HasSplash", GuRank >= 8 ? 1 : 0);
        description.Add("SplashReduction", GuRank >= 9 ? 6 : 4);
        description.Add("SplashDuration", GuRank >= 9 ? 2 : 1);
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        if (CombatState == null)
        {
            return;
        }

        Creature target = cardPlay.Target!;
        bool alreadySlowed = target.GetPower<HuanBuPower>() != null;
        (int reduction, int duration) = GuRank switch
        {
            3 => (4, 2),
            4 => (5, 2),
            5 => (6, 2),
            6 => (7, 2),
            7 => (8, 2),
            8 => (10, 2),
            _ => (12, 3),
        };

        await ApplyHuanBu(choiceContext, target, reduction, duration);
        if (alreadySlowed && GuRank >= 5)
        {
            await ZhouDaoPowerSystem.GainNianHua(
                choiceContext,
                this,
                GuRank >= 7 ? 2 : 1
            );
        }

        if (GuRank >= 8)
        {
            int splashReduction = GuRank >= 9 ? 6 : 4;
            int splashDuration = GuRank >= 9 ? 2 : 1;
            foreach (Creature enemy in CombatState.HittableEnemies
                         .Where(enemy => !ReferenceEquals(enemy, target)))
            {
                await ApplyHuanBu(
                    choiceContext,
                    enemy,
                    splashReduction,
                    splashDuration
                );
            }
        }
    }

    private async Task ApplyHuanBu(
        PlayerChoiceContext choiceContext,
        Creature target,
        int reduction,
        int duration
    )
    {
        HuanBuPower? power = await PowerCmd.Apply<HuanBuPower>(
            choiceContext,
            target,
            duration,
            Owner.Creature,
            this
        );
        power?.SetReduction(reduction);
    }
}
