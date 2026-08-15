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

    private void RefreshRankValues()
    {
        DynamicVars["Chance"].BaseValue =
            LiDaoRankTable.FeiXiongChance(GuRank);
        DynamicVars.Damage.BaseValue =
            LiDaoRankTable.FeiXiongDamage(GuRank);
        DynamicVars["DivineMight"].BaseValue =
            LiDaoRankTable.FeiXiongDivineMight(GuRank);
    }
}
