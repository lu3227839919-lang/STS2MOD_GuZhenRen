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
