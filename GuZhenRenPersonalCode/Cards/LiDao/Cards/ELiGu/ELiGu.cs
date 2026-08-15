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
public sealed class ELiGu : AbstractLiDaoBeastGuCard<EXuYing>
{
    public override int TrainingRequired => GuRank >= 6 ? 1 : 2;
    public override Type CompanionCardType => typeof(JiaoShuai);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Chance", 25m),
        new DamageVar(3m, ValueProp.Move),
        new DynamicVar("Hits", 2m),
    ];

    public ELiGu() : base(CardRarity.Uncommon) => RefreshRankValues();

    protected override void OnGuRankChanged()
    {
        base.OnGuRankChanged();
        RefreshRankValues();
    }

    internal static int ChanceAtRank(int rank) => rank switch
    {
        <= 1 => 25, 2 => 27, 3 => 30, 4 => 32, 5 => 35,
        6 => 38, 7 => 40, 8 => 43, _ => 45,
    };

    internal static int DamageAtRank(int rank) => rank switch
    {
        <= 1 => 3, 2 => 4, 3 => 4, 4 => 5, 5 => 5,
        6 => 6, 7 => 7, 8 => 8, _ => 8,
    };

    internal static int HitsAtRank(int rank) => rank switch
    {
        <= 4 => 2,
        <= 7 => 3,
        _ => 4,
    };

    internal static int HitBonusAtRank(int rank, int hitIndex) =>
        rank == 3 && hitIndex == 1 ? 2 : 0;

    internal static bool PursuesAtRank(int rank) => rank >= 7;

    private void RefreshRankValues()
    {
        DynamicVars["Chance"].BaseValue =
            ChanceAtRank(GuRank);
        DynamicVars.Damage.BaseValue = DamageAtRank(GuRank);
        DynamicVars["Hits"].BaseValue = HitsAtRank(GuRank);
    }
}
