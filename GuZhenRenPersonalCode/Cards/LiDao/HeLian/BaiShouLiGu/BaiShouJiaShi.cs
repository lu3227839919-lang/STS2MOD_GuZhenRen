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
public sealed class BaiShouJiaShi : AbstractLiDaoCompanionCard
{
    public override Type TrainedGuType => typeof(BaiShouLiGu);
    public override bool GainsBlock => true;

    private decimal _upDamage;
    private decimal _upBlock;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(7m, ValueProp.Move),
        new BlockVar(4m, ValueProp.Move),
    ];

    public BaiShouJiaShi() : base(CardType.Attack, TargetType.AnyEnemy)
    {
        RefreshRankValues();
    }

    private static int DamageAtRank(int rank) => rank switch
    {
        <= 1 => 7, 2 => 8, 3 => 9, 4 => 10, 5 => 11,
        6 => 12, 7 => 13, 8 => 15, _ => 17,
    };

    private static int BlockAtRank(int rank) => rank switch
    {
        <= 1 => 4, 2 => 4, 3 => 5, 4 => 5, 5 => 6,
        6 => 7, 7 => 8, 8 => 9, _ => 11,
    };

    private static int ExtraDamageAtRank(int rank) => rank switch
    {
        5 or 6 => 2,
        7 => 3,
        8 => 4,
        >= 9 => 5,
        _ => 0,
    };

    private static int BlockFourAtRank(int rank) => rank switch
    {
        8 => 3,
        >= 9 => 4,
        _ => 0,
    };

    protected override void RefreshRankValues()
    {
        DynamicVars.Damage.BaseValue =
            DamageAtRank(GuRank) + _upDamage;
        DynamicVars.Block.BaseValue =
            BlockAtRank(GuRank) + _upBlock;
    }

    protected override void AddExtraArgsToDescription(
        LocString description
    )
    {
        base.AddExtraArgsToDescription(description);
        int rank = GuRank;
        description.Add("ExtraDamageRange", rank >= 5 ? 1 : 0);
        description.Add(
            "ExtraDamage",
            ExtraDamageAtRank(rank)
        );
        description.Add("BlockFourRange", rank >= 8 ? 1 : 0);
        description.Add(
            "BlockFour",
            BlockFourAtRank(rank)
        );
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        int rank = GuRank;
        int kinds = PermanentPhantomKinds;

        decimal damage = DynamicVars.Damage.BaseValue;
        if (kinds >= 3)
        {
            damage += ExtraDamageAtRank(rank);
        }

        await DamageCmd.Attack(damage)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target!)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(choiceContext);

        if (kinds >= 2)
        {
            decimal block = DynamicVars.Block.BaseValue;
            if (kinds >= 4)
            {
                block += BlockFourAtRank(rank);
            }
            await CreatureCmd.GainBlock(
                Owner.Creature,
                block,
                ValueProp.Move,
                cardPlay
            );
        }
    }

    protected override void OnUpgrade()
    {
        _upDamage += 3m;
        _upBlock += 2m;
        RefreshRankValues();
    }
}
