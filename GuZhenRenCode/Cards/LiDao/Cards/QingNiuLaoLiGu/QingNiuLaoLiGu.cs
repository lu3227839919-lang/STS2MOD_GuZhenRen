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
    public override int RecoveryDelayTurns => LiDaoRankTable.Recovery(GuRank);

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

    private void RefreshRankValues()
    {
        DynamicVars["Chance"].BaseValue =
            LiDaoRankTable.QingNiuChance(GuRank);
        DynamicVars.Damage.BaseValue =
            LiDaoRankTable.QingNiuDamage(GuRank);
        DynamicVars.Block.BaseValue =
            LiDaoRankTable.QingNiuBlock(GuRank);
    }
}
