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
    public override Type CompanionCardType => typeof(ChenJianChong);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Chance", 30m),
        new DamageVar(5m, ValueProp.Move),
        new DynamicVar(LiDaoBeastTrainingSystem.ProgressVarName, 0m),
    ];

    public BaiZhiGu() : base(CardRarity.Common) => RefreshRankValues();

    protected override void OnGuRankChanged()
    {
        base.OnGuRankChanged();
        RefreshRankValues();
    }

    internal static int ChanceAtRank(int rank) => rank switch
    {
        <= 2 => 30,
        3 => 33,
        4 => 35,
        _ => 38,
    };

    internal static int DamageAtRank(int rank) => rank switch
    {
        <= 2 => 6,
        3 => 7,
        4 => 8,
        _ => 9,
    };

    internal static int FirstManifestBonusAtRank(int rank) => rank switch
    {
        4 => 2,
        >= 5 => 3,
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
