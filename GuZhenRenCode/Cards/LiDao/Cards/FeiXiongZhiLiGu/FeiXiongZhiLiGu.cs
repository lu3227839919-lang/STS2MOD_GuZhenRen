using GuZhenRen.Cards.HeLian;
using GuZhenRen.Characters;
using GuZhenRen.Combat;
using GuZhenRen.Powers.LiDao;

using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Utils;

namespace GuZhenRen.Cards.LiDao;

[RegisterCard(typeof(GuZhenRenGuCardPool))]
public sealed class FeiXiongZhiLiGu :
    AbstractLiDaoBeastGuCard<FeiXiongXuYing>
{
    public override int TrainingRequired => GuRank switch
    {
        <= 2 => 3,
        <= 6 => 2,
        _ => 1,
    };

    public override Type CompanionCardType => typeof(FeiXiongZhuang);
    public override int RecoveryDelayTurns => GuRank switch
    {
        <= 4 => 2,
        <= 8 => 3,
        _ => 4,
    };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Chance", 18m),
        new DamageVar(10m, ValueProp.Move),
        new DynamicVar("DivineMight", 0m),
    ];

    public FeiXiongZhiLiGu() : base(CardRarity.Rare) => RefreshRankValues();

    protected override void OnGuRankChanged()
    {
        base.OnGuRankChanged();
        RefreshRankValues();
    }

    internal static int ChanceAtRank(int rank) => rank switch
    {
        <= 1 => 18, 2 => 20, 3 => 22, 4 => 25, 5 => 28,
        6 => 30, 7 => 33, 8 => 36, _ => 40,
    };

    internal static int DamageAtRank(int rank) => rank switch
    {
        <= 1 => 10, 2 => 12, 3 => 14, 4 => 16, 5 => 19,
        6 => 24, 7 => 28, 8 => 34, _ => 40,
    };

    internal static int DivineMightAtRank(int rank) => rank switch
    {
        <= 5 => 0,
        6 => 6,
        7 => 8,
        8 => 10,
        _ => 12,
    };

    internal static int BlockedTargetBonusAtRank(int rank) => rank switch
    {
        3 => 4,
        4 => 5,
        5 => 6,
        _ => 0,
    };

    internal static decimal FirstManifestMultiplierAtRank(
        int rank,
        bool firstThisTurn
    ) => rank >= 9 && firstThisTurn ? 1.5m : 1m;

    internal static int QuakeDamageAtRank(int rank) => rank >= 8 ? 6 : 0;

    private void RefreshRankValues()
    {
        DynamicVars["Chance"].BaseValue =
            ChanceAtRank(GuRank);
        DynamicVars.Damage.BaseValue =
            DamageAtRank(GuRank);
        DynamicVars["DivineMight"].BaseValue =
            DivineMightAtRank(GuRank);
    }
}
