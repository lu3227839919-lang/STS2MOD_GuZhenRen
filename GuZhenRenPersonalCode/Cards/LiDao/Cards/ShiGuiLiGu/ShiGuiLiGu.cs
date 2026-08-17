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
    public override Type CompanionCardType => typeof(ChenZhuang);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Chance", 30m),
        new BlockVar(4m, ValueProp.Move),
        new DynamicVar(LiDaoBeastTrainingSystem.ProgressVarName, 0m),
    ];

    public ShiGuiLiGu() : base(CardRarity.Uncommon) => RefreshRankValues();

    protected override void OnGuRankChanged()
    {
        base.OnGuRankChanged();
        RefreshRankValues();
    }

    internal static int ChanceAtRank(int rank) => rank switch
    {
        <= 2 => 30,
        3 => 33,
        4 => 36,
        _ => 40,
    };

    internal static int BlockAtRank(int rank) => rank switch
    {
        <= 2 => 6,
        3 => 8,
        4 => 9,
        _ => 11,
    };

    internal static int NoBlockBonusAtRank(int rank) => rank switch
    {
        4 => 2,
        >= 5 => 3,
        _ => 0,
    };

    private void RefreshRankValues()
    {
        DynamicVars["Chance"].BaseValue =
            ChanceAtRank(GuRank);
        DynamicVars.Block.BaseValue =
            BlockAtRank(GuRank);
    }
}
