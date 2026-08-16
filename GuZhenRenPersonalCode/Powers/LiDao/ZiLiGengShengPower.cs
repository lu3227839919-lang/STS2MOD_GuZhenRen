using System.Numerics;

using GuZhenRen.Cards;
using GuZhenRen.Cards.LiDao;

using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Powers.LiDao;

[RegisterPower]
public sealed class ZiLiGengShengPower : ModPowerTemplate
{
    private int _trackedTurn;
    private int _manifestedMask;
    private int _lifeForce;
    private bool _immediateHealUsed;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override int DisplayAmount => StrongBodyStacks;

    public int Rank => DynamicVars["Rank"].IntValue;
    public int StrongBodyStacks => Math.Clamp(Amount - 1, 0, 3);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("Rank", 1m)];

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://GuZhenRenPersonal/images/power/ZiLiGengShengPower-64x64.png",
        BigIconPath: "res://GuZhenRenPersonal/images/power/ZiLiGengShengPower-256x256.png"
    );

    internal void ConfigureRank(int rank)
    {
        DynamicVars["Rank"].BaseValue = Math.Clamp(rank, 1, 9);
        InvokeDisplayAmountChanged();
    }

    internal async Task RecordManifestationAsync(LiDaoBeastKind kind)
    {
        ResetTurnStateIfNeeded();
        _manifestedMask |= 1 << (int)kind;

        int immediateHeal = _immediateHealUsed
            ? 0
            : ZiLiGengShengGu.ImmediateHealAtRank(
                Rank,
                LiDaoPhantomSystem.GetForceBase(Owner.Player!)
            );

        if (immediateHeal > 0)
        {
            _immediateHealUsed = true;
            await HealWithOverflow(
                new ThrowingPlayerChoiceContext(),
                immediateHeal
            );
        }
    }

    internal void RecordCondensation()
    {
        ResetTurnStateIfNeeded();
        int cap = ZiLiGengShengGu.LifeForceCapAtRank(Rank);
        if (cap > 0)
        {
            _lifeForce = Math.Min(cap, _lifeForce + 1);
        }
    }

    public override async Task BeforeSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants
    )
    {
        if (!participants.Contains(Owner) ||
            !ZiLiGengShengGu.HealsAtTurnEndAtRank(Rank))
        {
            return;
        }

        ResetTurnStateIfNeeded();
        int forceBase = LiDaoPhantomSystem.GetForceBase(Owner.Player!);
        int cap = ZiLiGengShengGu.HealingCapAtRank(Rank) + StrongBodyStacks;
        int healing = Math.Min(forceBase, cap);

        healing += ZiLiGengShengGu.ForceBaseBonusAtRank(Rank, forceBase);
        healing += ZiLiGengShengGu.LifeForceBonusAtRank(Rank, _lifeForce);

        int manifestedKinds = BitOperations.PopCount((uint)_manifestedMask);
        int hardship = Owner.GetPower<KuLiPower>()?.Hardship ?? 0;
        healing += ZiLiGengShengGu.ManifestationBonusAtRank(
            Rank,
            manifestedKinds,
            hardship
        );

        decimal multiplier = ZiLiGengShengGu.LowHealthMultiplierAtRank(
            Rank,
            Owner.CurrentHp,
            Owner.MaxHp
        );
        if (multiplier != 1m)
        {
            healing = (int)Math.Round(
                healing * multiplier,
                MidpointRounding.AwayFromZero
            );
        }

        if (healing > 0)
        {
            await HealWithOverflow(choiceContext, healing);
        }

        ResetTurnState(force: true);
    }

    private async Task HealWithOverflow(
        PlayerChoiceContext choiceContext,
        int requested
    )
    {
        int missing = Math.Max(0, Owner.MaxHp - Owner.CurrentHp);
        int healed = Math.Min(missing, requested);
        if (healed > 0)
        {
            await CreatureCmd.Heal(Owner, healed);
        }

        int overflow = Math.Max(0, requested - healed);
        int block = ZiLiGengShengGu.OverflowBlockAtRank(Rank, overflow);
        if (block > 0)
        {
            await CreatureCmd.GainBlock(
                Owner,
                block,
                ValueProp.Unpowered,
                cardPlay: null
            );
        }
    }

    private void ResetTurnStateIfNeeded()
    {
        int turn = Owner.Player?.PlayerCombatState?.TurnNumber ?? 1;
        if (_trackedTurn != turn)
        {
            ResetTurnState(force: true);
            _trackedTurn = turn;
        }
    }

    private void ResetTurnState(bool force)
    {
        if (!force)
        {
            return;
        }
        _manifestedMask = 0;
        _lifeForce = 0;
        _immediateHealUsed = false;
    }
}
