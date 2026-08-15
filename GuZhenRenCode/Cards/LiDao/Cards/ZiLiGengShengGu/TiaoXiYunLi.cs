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

    protected override void RefreshRankValues()
    {
        DynamicVars.Block.BaseValue =
            LiDaoCompanionRankTable.TiaoXiYunLiBlock(GuRank) + _upBlock;
        DynamicVars["PhantomBonus"].BaseValue =
            LiDaoCompanionRankTable.TiaoXiYunLiPhantomBonus(GuRank) +
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
                LiDaoCompanionRankTable.TiaoXiYunLiHeal(rank, 3)
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

        if (LiDaoCompanionRankTable.TiaoXiYunLiCanHeal(rank))
        {
            int heal = LiDaoCompanionRankTable.TiaoXiYunLiHeal(rank, kinds);
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
