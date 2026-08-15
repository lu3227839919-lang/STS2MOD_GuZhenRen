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
public sealed class ChenZhuang : AbstractLiDaoCompanionCard
{
    public override Type TrainedGuType => typeof(ShiGuiLiGu);
    public override bool GainsBlock => true;

    private decimal _upBlock;
    private decimal _upAttackBonus;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(8m, ValueProp.Move),
        new DynamicVar("AttackBonus", 2m),
    ];

    public ChenZhuang() : base(CardType.Skill, TargetType.Self)
    {
        RefreshRankValues();
    }

    protected override void RefreshRankValues()
    {
        DynamicVars.Block.BaseValue =
            LiDaoCompanionRankTable.ChenZhuangBlock(GuRank) + _upBlock;
        DynamicVars["AttackBonus"].BaseValue =
            LiDaoCompanionRankTable.ChenZhuangAttackBonus(GuRank) +
            _upAttackBonus;
    }

    protected override void AddExtraArgsToDescription(
        LocString description
    )
    {
        base.AddExtraArgsToDescription(description);
        int rank = GuRank;
        description.Add("NoBlockRange", rank is >= 5 and <= 8 ? 1 : 0);
        description.Add(
            "NoBlockBonus",
            LiDaoCompanionRankTable.ChenZhuangNoBlockBonus(rank)
        );
    }

    protected override Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        int rank = GuRank;
        decimal block = DynamicVars.Block.BaseValue;
        decimal attackBonus = PlayedAttackEarlierThisTurn()
            ? DynamicVars["AttackBonus"].BaseValue
            : 0m;

        if (Owner.Creature.Block <= 0)
        {
            if (rank <= 8)
            {
                block += LiDaoCompanionRankTable
                    .ChenZhuangNoBlockBonus(rank);
            }
            else
            {
                block = Math.Round(
                    block * 1.5m,
                    MidpointRounding.AwayFromZero
                );
            }
        }

        block += attackBonus;

        return CreatureCmd.GainBlock(
            Owner.Creature,
            block,
            ValueProp.Move,
            cardPlay
        );
    }

    protected override void OnUpgrade()
    {
        _upBlock += 3m;
        _upAttackBonus += 1m;
        RefreshRankValues();
    }
}
