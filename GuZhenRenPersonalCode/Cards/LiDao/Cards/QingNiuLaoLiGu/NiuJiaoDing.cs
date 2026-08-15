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
public sealed class NiuJiaoDing : AbstractLiDaoCompanionCard
{
    public override Type TrainedGuType => typeof(QingNiuLaoLiGu);
    public override bool GainsBlock => true;

    private decimal _upDamage;
    private decimal _upBlock;

    /// <summary>本场首次打出记录（战斗实例级别，随战斗克隆重置）。</summary>
    private sealed class FirstTimeState
    {
        internal bool Played;
    }

    private static readonly ConditionalWeakTable<CardModel, FirstTimeState>
        FirstTimeStates = new();

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(6m, ValueProp.Move),
        new BlockVar(3m, ValueProp.Move),
    ];

    public NiuJiaoDing() : base(CardType.Attack, TargetType.AnyEnemy)
    {
        RefreshRankValues();
    }

    private static int DamageAtRank(int rank) => rank switch
    {
        <= 1 => 6, 2 => 7, 3 => 8, 4 => 9, 5 => 10,
        6 => 11, 7 => 12, 8 => 14, _ => 16,
    };

    private static int BlockAtRank(int rank) => rank switch
    {
        <= 1 => 3, 2 => 3, 3 => 4, 4 => 5, 5 => 6,
        6 => 7, 7 => 8, 8 => 10, _ => 11,
    };

    private static int FirstTimeBonusAtRank(int rank) => rank >= 8 ? 3 : 0;

    private static int PhantomLinkBonusAtRank(int rank) => rank >= 9 ? 5 : 0;

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
        description.Add("FirstTimeRange", rank >= 8 ? 1 : 0);
        description.Add(
            "FirstTimeBonus",
            FirstTimeBonusAtRank(rank)
        );
        description.Add("PhantomLinkRange", rank >= 9 ? 1 : 0);
        description.Add(
            "PhantomLinkBonus",
            PhantomLinkBonusAtRank(rank)
        );
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        int rank = GuRank;

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target!)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(choiceContext);

        decimal block = DynamicVars.Block.BaseValue;
        if (rank >= 8 && TryClaimFirstTime())
        {
            block += FirstTimeBonusAtRank(rank);
        }
        if (rank >= 9 &&
            LiDaoPhantomSystem.HasManifestedThisTurn(Owner))
        {
            block += PhantomLinkBonusAtRank(rank);
        }

        await CreatureCmd.GainBlock(
            Owner.Creature,
            block,
            ValueProp.Move,
            cardPlay
        );
    }

    private bool TryClaimFirstTime()
    {
        FirstTimeState state = FirstTimeStates.GetValue(
            this,
            static _ => new FirstTimeState()
        );
        if (state.Played)
        {
            return false;
        }
        state.Played = true;
        return true;
    }

    protected override void OnUpgrade()
    {
        _upDamage += 2m;
        _upBlock += 2m;
        RefreshRankValues();
    }
}
