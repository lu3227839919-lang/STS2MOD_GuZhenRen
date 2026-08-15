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
public sealed class BaiZhiGu :
    AbstractLiDaoBeastGuCard<BaiZhiXuYing>
{
    public override int TrainingRequired => GuRank >= 5 ? 1 : 2;
    public override Type CompanionCardType => typeof(ChenJianChong);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Chance", 30m),
        new DamageVar(5m, ValueProp.Move),
    ];

    public BaiZhiGu() : base(CardRarity.Common) => RefreshRankValues();

    protected override void OnGuRankChanged()
    {
        base.OnGuRankChanged();
        RefreshRankValues();
    }

    internal static int ChanceAtRank(int rank) => rank switch
    {
        <= 1 => 30, 2 => 32, 3 => 35, 4 => 38, 5 => 40,
        6 => 45, 7 => 48, 8 => 55, _ => 60,
    };

    internal static int DamageAtRank(int rank) => rank switch
    {
        <= 1 => 5, 2 => 6, 3 => 7, 4 => 8, 5 => 10,
        6 => 12, 7 => 14, 8 => 17, _ => 20,
    };

    internal static int FirstManifestBonusAtRank(int rank) => rank switch
    {
        3 or 4 => 2,
        5 => 3,
        _ => 0,
    };

    internal static int NoBlockBonusAtRank(int rank) => rank switch
    {
        7 => 3,
        8 => 4,
        >= 9 => 5,
        _ => 0,
    };

    private void RefreshRankValues()
    {
        DynamicVars["Chance"].BaseValue =
            ChanceAtRank(GuRank);
        DynamicVars.Damage.BaseValue =
            DamageAtRank(GuRank);
    }
}
