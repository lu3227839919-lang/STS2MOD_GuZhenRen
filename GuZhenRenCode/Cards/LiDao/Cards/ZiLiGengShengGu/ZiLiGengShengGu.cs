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
public sealed class ZiLiGengShengGu : AbstractLiDaoGuCard
{
    public override int TrainingRequired => GuRank switch
    {
        <= 2 => 3,
        <= 5 => 2,
        _ => 1,
    };

    public override Type CompanionCardType => typeof(TiaoXiYunLi);
    public override int RecoveryDelayTurns => LiDaoRankTable.Recovery(GuRank);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [new HealVar(1m)];

    public ZiLiGengShengGu() : base(CardRarity.Rare) => RefreshRankValues();

    protected override Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    ) => LiDaoPowerSystem.ActivateZiLiAsync(
        choiceContext,
        this
    );

    protected override void OnGuRankChanged()
    {
        base.OnGuRankChanged();
        RefreshRankValues();
    }

    private void RefreshRankValues() => DynamicVars.Heal.BaseValue =
        LiDaoRankTable.ZiLiHealingCap(GuRank);
}
