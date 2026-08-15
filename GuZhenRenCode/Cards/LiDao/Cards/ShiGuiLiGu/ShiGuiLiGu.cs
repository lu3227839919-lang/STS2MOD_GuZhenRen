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
public sealed class ShiGuiLiGu :
    AbstractLiDaoBeastGuCard<ShiGuiXuYing>
{
    public override int TrainingRequired => GuRank >= 6 ? 1 : 2;
    public override Type CompanionCardType => typeof(ChenZhuang);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Chance", 30m),
        new BlockVar(4m, ValueProp.Move),
    ];

    public ShiGuiLiGu() : base(CardRarity.Uncommon) => RefreshRankValues();

    protected override void OnGuRankChanged()
    {
        base.OnGuRankChanged();
        RefreshRankValues();
    }

    internal static int ChanceAtRank(int rank) => rank switch
    {
        <= 1 => 30, 2 => 32, 3 => 35, 4 => 38, 5 => 40,
        6 => 43, 7 => 45, 8 => 48, _ => 50,
    };

    internal static int BlockAtRank(int rank) => rank switch
    {
        <= 1 => 4, 2 => 5, 3 => 6, 4 => 7, 5 => 9,
        6 => 11, 7 => 13, 8 => 15, _ => 18,
    };

    internal static int NoBlockBonusAtRank(int rank) => rank switch
    {
        5 => 2,
        6 => 3,
        7 => 4,
        _ => 0,
    };

    internal static int FirstManifestFlatBonusAtRank(int rank) =>
        rank == 8 ? 5 : 0;

    internal static decimal FirstManifestMultiplierAtRank(
        int rank,
        bool firstThisTurn
    ) => rank >= 9 && firstThisTurn ? 1.5m : 1m;

    private void RefreshRankValues()
    {
        DynamicVars["Chance"].BaseValue =
            ChanceAtRank(GuRank);
        DynamicVars.Block.BaseValue =
            BlockAtRank(GuRank);
    }
}
