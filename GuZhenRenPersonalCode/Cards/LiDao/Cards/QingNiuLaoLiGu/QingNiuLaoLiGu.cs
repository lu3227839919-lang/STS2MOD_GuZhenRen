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
public sealed class QingNiuLaoLiGu :
    AbstractLiDaoBeastGuCard<QingNiuXuYing>
{
    public override Type CompanionCardType => typeof(NiuJiaoDing);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Chance", 35m),
        new DamageVar(4m, ValueProp.Move),
        new BlockVar(2m, ValueProp.Move),
        new DynamicVar(LiDaoBeastTrainingSystem.ProgressVarName, 0m),
    ];

    public QingNiuLaoLiGu() : base(CardRarity.Common) => RefreshRankValues();

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

    internal static int DamageAtRank(int rank) => rank switch
    {
        <= 2 => 4,
        3 => 5,
        4 => 6,
        _ => 7,
    };

    internal static int BlockAtRank(int rank) => rank switch
    {
        <= 2 => 2,
        3 => 3,
        4 => 4,
        _ => 5,
    };

    internal static int HitBlockBonusAtRank(int rank) => rank switch
    {
        4 => 1,
        >= 5 => 2,
        _ => 0,
    };

    private void RefreshRankValues()
    {
        DynamicVars["Chance"].BaseValue =
            ChanceAtRank(GuRank);
        DynamicVars.Damage.BaseValue =
            DamageAtRank(GuRank);
        DynamicVars.Block.BaseValue =
            BlockAtRank(GuRank);
    }
}
