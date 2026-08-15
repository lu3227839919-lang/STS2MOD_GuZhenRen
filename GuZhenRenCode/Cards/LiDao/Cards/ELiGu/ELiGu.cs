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
    public override int RecoveryDelayTurns => LiDaoRankTable.Recovery(GuRank);

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

    private void RefreshRankValues()
    {
        DynamicVars["Chance"].BaseValue =
            LiDaoRankTable.EChance(GuRank);
        DynamicVars.Damage.BaseValue = LiDaoRankTable.EDamage(GuRank);
        DynamicVars["Hits"].BaseValue = LiDaoRankTable.EHits(GuRank);
    }
}
