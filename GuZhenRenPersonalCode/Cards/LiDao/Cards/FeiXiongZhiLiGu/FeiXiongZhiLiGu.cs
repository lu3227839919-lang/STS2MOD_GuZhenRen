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
    public override Type CompanionCardType => typeof(FeiXiongZhuang);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Chance", 22m),
        new DamageVar(9m, ValueProp.Move),
        new DynamicVar("BlockBonus", 3m),
    ];

    public FeiXiongZhiLiGu() : base(CardRarity.Rare) => RefreshRankValues();

    protected override void OnGuRankChanged()
    {
        base.OnGuRankChanged();
        RefreshRankValues();
    }

    internal static int ChanceAtRank(int rank) => rank switch
    {
        <= 2 => 22,
        3 => 24,
        4 => 26,
        _ => 28,
    };

    internal static int DamageAtRank(int rank) => rank switch
    {
        <= 2 => 9,
        3 => 11,
        4 => 13,
        _ => 15,
    };

    internal static int BlockedTargetBonusAtRank(int rank) => rank switch
    {
        <= 2 => 3,
        3 => 4,
        4 => 5,
        _ => 6,
    };

    private void RefreshRankValues()
    {
        DynamicVars["Chance"].BaseValue =
            ChanceAtRank(GuRank);
        DynamicVars.Damage.BaseValue =
            DamageAtRank(GuRank);
        DynamicVars["BlockBonus"].BaseValue =
            BlockedTargetBonusAtRank(GuRank);
    }
}
