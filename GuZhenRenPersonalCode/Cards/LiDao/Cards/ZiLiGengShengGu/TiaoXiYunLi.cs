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
public sealed class TiaoXiYunLi : AbstractLiDaoCompanionCard
{
    public override Type TrainedGuType => typeof(ZiLiGengShengGu);
    public override bool GainsBlock => true;

    private decimal _upBlock;
    private decimal _upPhantomBonus;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(7m, ValueProp.Move),
        new DynamicVar("PhantomBonus", 3m),
    ];

    public TiaoXiYunLi() : base(CardType.Skill, TargetType.Self)
    {
        RefreshRankValues();
    }

    private static int BlockAtRank(int rank) => rank switch
    {
        <= 1 => 7, 2 => 8, 3 => 9, 4 => 10, 5 => 11,
        6 => 12, 7 => 13, 8 => 14, _ => 16,
    };

    private static int PhantomBonusAtRank(int rank) => rank switch
    {
        <= 2 => 3,
        <= 4 => 4,
        <= 6 => 5,
        <= 8 => 6,
        _ => 7,
    };

    private static int HealAtRank(int rank, int phantomKinds) => rank switch
    {
        6 or 7 => phantomKinds >= 1 ? 1 : 0,
        8 => phantomKinds >= 2 ? 2 : 0,
        >= 9 => Math.Min(phantomKinds, 3),
        _ => 0,
    };

    private static bool CanHealAtRank(int rank) => rank >= 6;

    protected override void RefreshRankValues()
    {
        DynamicVars.Block.BaseValue =
            BlockAtRank(GuRank) + _upBlock;
        DynamicVars["PhantomBonus"].BaseValue =
            PhantomBonusAtRank(GuRank) +
            _upPhantomBonus;
    }

    protected override void AddExtraArgsToDescription(
        LocString description
    )
    {
        base.AddExtraArgsToDescription(description);
        int rank = GuRank;
        description.Add("HealBasicRange", rank is >= 6 and <= 7 ? 1 : 0);
        description.Add("HealTwoRange", rank == 8 ? 1 : 0);
        if (rank is >= 6 and <= 8)
        {
            description.Add(
                "Heal",
                HealAtRank(rank, 3)
            );
        }
        if (rank >= 9)
        {
            description.Add("Heal1", 1);
            description.Add("Heal2", 2);
            description.Add("Heal3", 3);
        }
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        int rank = GuRank;
        int kinds = PermanentPhantomKinds;

        decimal block = DynamicVars.Block.BaseValue;
        if (kinds > 0)
        {
            block += DynamicVars["PhantomBonus"].BaseValue;
        }

        await CreatureCmd.GainBlock(
            Owner.Creature,
            block,
            ValueProp.Move,
            cardPlay
        );

        if (CanHealAtRank(rank))
        {
            int heal = HealAtRank(rank, kinds);
            if (heal > 0)
            {
                await CreatureCmd.Heal(Owner.Creature, heal);
            }
        }
    }

    protected override void OnUpgrade()
    {
        _upBlock += 3m;
        _upPhantomBonus += 1m;
        RefreshRankValues();
    }
}
