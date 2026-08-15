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
    public override int TrainingRequired => GuRank >= 5 ? 1 : 2;
    public override Type CompanionCardType => typeof(NiuJiaoDing);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Chance", 35m),
        new DamageVar(4m, ValueProp.Move),
        new BlockVar(2m, ValueProp.Move),
    ];

    public QingNiuLaoLiGu() : base(CardRarity.Common) => RefreshRankValues();

    protected override void OnGuRankChanged()
    {
        base.OnGuRankChanged();
        RefreshRankValues();
    }

    internal static int ChanceAtRank(int rank) => rank switch
    {
        <= 1 => 35, 2 => 37, 3 => 40, 4 => 42, 5 => 45,
        6 => 48, 7 => 50, 8 => 53, _ => 55,
    };

    internal static int DamageAtRank(int rank) => rank switch
    {
        <= 1 => 4, 2 => 5, 3 => 6, 4 => 7, 5 => 8,
        6 => 10, 7 => 12, 8 => 14, _ => 17,
    };

    internal static int BlockAtRank(int rank) => rank switch
    {
        <= 1 => 2, 2 => 2, 3 => 3, 4 => 4, 5 => 5,
        6 => 6, 7 => 7, 8 => 9, _ => 11,
    };

    internal static int FirstManifestBlockBonusAtRank(int rank) =>
        rank >= 8 ? 3 : 0;

    internal static int PhantomLinkBlockBonusAtRank(int rank) =>
        rank >= 9 ? 5 : 0;

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
