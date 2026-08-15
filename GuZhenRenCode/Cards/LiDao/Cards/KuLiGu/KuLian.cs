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
public sealed class KuLian : AbstractLiDaoCompanionCard
{
    public override Type TrainedGuType => typeof(KuLiGu);
    public override bool GainsBlock => true;

    private decimal _upBlock;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(12m, ValueProp.Move),
        new DynamicVar("HpLoss", 2m),
    ];

    public KuLian() : base(CardType.Skill, TargetType.Self)
    {
        RefreshRankValues();
    }

    private static int BlockAtRank(int rank) => rank switch
    {
        <= 1 => 12, 2 => 13, 3 => 14, 4 => 15, 5 => 16,
        6 => 18, 7 => 19, 8 => 21, _ => 23,
    };

    private static int HardshipBonusAtRank(int rank) => rank switch
    {
        5 or 6 or 7 => 1,
        >= 8 => 2,
        _ => 0,
    };

    protected override void RefreshRankValues() =>
        DynamicVars.Block.BaseValue =
            BlockAtRank(GuRank) + _upBlock;

    protected override void AddExtraArgsToDescription(
        LocString description
    )
    {
        base.AddExtraArgsToDescription(description);
        int rank = GuRank;
        description.Add("HardshipRange", rank >= 5 ? 1 : 0);
        description.Add(
            "HardshipBonus",
            HardshipBonusAtRank(rank)
        );
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        int hpLoss = Math.Min(
            DynamicVars["HpLoss"].IntValue,
            Math.Max(0, Owner.Creature.CurrentHp - 1)
        );

        if (hpLoss > 0)
        {
            await CreatureCmd.Damage(
                choiceContext,
                Owner.Creature,
                hpLoss,
                ValueProp.Unblockable | ValueProp.Unpowered,
                dealer: null,
                cardSource: this,
                cardPlay: cardPlay
            );
        }

        decimal block = DynamicVars.Block.BaseValue;
        int rank = GuRank;
        if (rank >= 5)
        {
            int hardship =
                Owner.Creature.GetPower<KuLiPower>()?.Hardship ?? 0;
            block += hardship *
                HardshipBonusAtRank(rank);
        }

        await CreatureCmd.GainBlock(
            Owner.Creature,
            block,
            ValueProp.Move,
            cardPlay
        );
    }

    protected override void OnUpgrade()
    {
        _upBlock += 4m;
        RefreshRankValues();
    }
}
