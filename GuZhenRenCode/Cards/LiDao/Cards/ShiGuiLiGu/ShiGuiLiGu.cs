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
    public override int TrainingRequired => GuRank >= 6 ? 1 : 2;
    public override Type CompanionCardType => typeof(ChenZhuang);
    public override int RecoveryDelayTurns => LiDaoRankTable.Recovery(GuRank);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Chance", 30m),
        new BlockVar(4m, ValueProp.Move),
    ];

    public ShiGuiLiGu() : base(CardRarity.Uncommon) => RefreshRankValues();

    protected override void OnGuRankChanged()
    {
        base.OnGuRankChanged();
        RefreshRankValues();
    }

    private void RefreshRankValues()
    {
        DynamicVars["Chance"].BaseValue =
            LiDaoRankTable.ShiGuiChance(GuRank);
        DynamicVars.Block.BaseValue =
            LiDaoRankTable.ShiGuiBlock(GuRank);
    }
}
