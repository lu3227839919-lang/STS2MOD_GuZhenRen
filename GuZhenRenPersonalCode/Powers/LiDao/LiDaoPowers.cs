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
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Powers.LiDao;

public static class LiDaoPowerSystem
{
    public static async Task ActivateKuLiAsync(
        PlayerChoiceContext choiceContext,
        KuLiGu source
    )
    {
        KuLiPower? existing = source.Owner.Creature.GetPower<KuLiPower>();
        if (existing == null)
        {
            KuLiPower power =
                (KuLiPower)ModelDb.Power<KuLiPower>().ToMutable();
            power.ConfigureRank(source.GuRank);
            await PowerCmd.Apply(
                choiceContext,
                power,
                source.Owner.Creature,
                1,
                source.Owner.Creature,
                source
            );
            return;
        }

        existing.ConfigureRank(Math.Max(existing.Rank, source.GuRank));
        if (existing.GrindingStacks < 3)
        {
            await PowerCmd.ModifyAmount(
                choiceContext,
                existing,
                1,
                source.Owner.Creature,
                source
            );
        }
    }

    public static async Task ActivateZiLiAsync(
        PlayerChoiceContext choiceContext,
        ZiLiGengShengGu source
    )
    {
        ZiLiGengShengPower? existing =
            source.Owner.Creature.GetPower<ZiLiGengShengPower>();
        if (existing == null)
        {
            ZiLiGengShengPower power =
                (ZiLiGengShengPower)
                    ModelDb.Power<ZiLiGengShengPower>().ToMutable();
            power.ConfigureRank(source.GuRank);
            await PowerCmd.Apply(
                choiceContext,
                power,
                source.Owner.Creature,
                1,
                source.Owner.Creature,
                source
            );
            return;
        }

        existing.ConfigureRank(Math.Max(existing.Rank, source.GuRank));
        if (existing.StrongBodyStacks < 3)
        {
            await PowerCmd.ModifyAmount(
                choiceContext,
                existing,
                1,
                source.Owner.Creature,
                source
            );
        }
    }

    public static decimal GetBeastEffectMultiplier(Creature owner) =>
        owner.GetPower<KuLiPower>()?.EffectMultiplier ?? 1m;

    public static Task NotifyManifested(
        Creature owner,
        LiDaoBeastKind kind
    ) => owner.GetPower<ZiLiGengShengPower>() is { } power
        ? power.RecordManifestationAsync(kind)
        : Task.CompletedTask;

    public static void NotifyCondensed(Creature owner) =>
        owner.GetPower<ZiLiGengShengPower>()?.RecordCondensation();
}

[RegisterPower]
public sealed class LiDaoBattlePower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    protected override bool IsVisibleInternal => false;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://GuZhenRenPersonal/images/power/LiDaoBattlePower-64x64.png",
        BigIconPath: "res://GuZhenRenPersonal/images/power/LiDaoBattlePower-256x256.png"
    );

    public override Task AfterCardPlayed(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    ) => ReferenceEquals(cardPlay.Player.Creature, Owner)
        ? LiDaoPhantomSystem.ResolveAttackAsync(choiceContext, cardPlay)
        : Task.CompletedTask;
}

[RegisterPower]
public sealed class KuLiPower :
    ModPowerTemplate,
    IProbabilityModifier
{
    private int _lastDesperationTurn;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override int DisplayAmount => GrindingStacks;

    public int Rank => DynamicVars["Rank"].IntValue;
    public int GrindingStacks => Math.Clamp(Amount - 1, 0, 3);

    public bool CanRetryAllFailed =>
        KuLiGu.CanRetryAllFailedAtRank(Rank, Hardship);
    public bool CanCreateDoubleShadow =>
        KuLiGu.CanCreateDoubleShadowAtRank(Rank, Hardship);

    public decimal EffectMultiplier => 1m +
        GetHardshipEffectBonus() +
        GrindingStacks * 0.05m;

    public int Hardship
    {
        get
        {
            int maxHp = Math.Max(1, Owner.MaxHp);
            int lostPercent = (maxHp - Owner.CurrentHp) * 100 / maxHp;
            return lostPercent switch
            {
                >= 80 => 4,
                >= 60 => 3,
                >= 40 => 2,
                >= 20 => 1,
                _ => 0,
            };
        }
    }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("Rank", 1m)];

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://GuZhenRenPersonal/images/power/KuLiPower-64x64.png",
        BigIconPath: "res://GuZhenRenPersonal/images/power/KuLiPower-256x256.png"
    );

    internal void ConfigureRank(int rank)
    {
        DynamicVars["Rank"].BaseValue = Math.Clamp(rank, 1, 9);
        InvokeDisplayAmountChanged();
    }

    public float GetAdditiveProbability(CardModel card)
    {
        if (card is not AbstractLiDaoXuYing phantom ||
            phantom.IsFullForcePhantom)
        {
            return 0f;
        }

        int perHardship = KuLiGu.ChancePerHardshipAtRank(Rank);
        return Hardship * perHardship / 100f;
    }

    internal bool TryClaimDesperationBurst(int turn)
    {
        if (!KuLiGu.CanDesperationBurstAtRank(Rank, Hardship) ||
            _lastDesperationTurn == turn)
        {
            return false;
        }

        _lastDesperationTurn = turn;
        return true;
    }

    private decimal GetHardshipEffectBonus() =>
        KuLiGu.HardshipEffectBonusAtRank(Rank, Hardship);
}

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
