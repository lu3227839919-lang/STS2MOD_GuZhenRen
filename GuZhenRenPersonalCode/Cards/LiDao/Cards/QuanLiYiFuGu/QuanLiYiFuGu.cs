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
public sealed class QuanLiYiFuGu : AbstractLiDaoGuCard
{
    public override int YuanQiCost => 2;

    public override int TrainingRequired => GuRank switch
    {
        <= 3 => 3,
        <= 7 => 2,
        _ => 1,
    };

    public override Type CompanionCardType => typeof(YunLi);
    public override int RecoveryDelayTurns => GuRank >= 9 ? 4 : 3;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [new DynamicVar("EffectPercent", 100m)];

    public QuanLiYiFuGu() : base(CardRarity.Rare) => RefreshRankValues();

    protected override Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    ) => LiDaoPhantomSystem.ActivateFullForceGuAsync(
        choiceContext,
        this
    );

    public override IReadOnlyList<CardModel> GetCarouselCards() =>
        [GuCardReferenceFactory.Create<QuanLiXuYing>(this)];

    protected override void OnGuRankChanged()
    {
        base.OnGuRankChanged();
        RefreshRankValues();
    }

    internal static int ForcedPhantomLimitAtRank(
        int rank,
        int availableCount
    ) => rank switch
    {
        <= 1 => Math.Min(1, availableCount),
        2 => Math.Min(2, availableCount),
        _ => availableCount,
    };

    internal static bool UsesRandomSubsetAtRank(int rank) => rank <= 2;

    internal static float PermanentChanceGainAtRank(int rank) => rank switch
    {
        8 => 0.03f,
        >= 9 => 0.05f,
        _ => 0f,
    };

    internal static int RecoveryAccelerationAtRank(int rank) =>
        rank >= 9 ? 1 : 0;

    private void RefreshRankValues() =>
        DynamicVars["EffectPercent"].BaseValue =
            EffectPercentAtRank(GuRank);

    internal static int EffectPercentAtRank(int rank) => rank switch
    {
        <= 2 => 100,
        3 => 80,
        4 => 90,
        5 => 100,
        6 => 110,
        7 => 120,
        8 => 130,
        _ => 140,
    };

}
