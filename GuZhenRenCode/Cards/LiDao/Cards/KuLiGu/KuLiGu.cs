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
public sealed class KuLiGu : AbstractLiDaoGuCard
{
    public override int TrainingRequired => GuRank >= 5 ? 1 : 2;
    public override Type CompanionCardType => typeof(KuLian);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [new DynamicVar("ChancePerHardship", 2m)];

    public KuLiGu() : base(CardRarity.Rare) => RefreshRankValues();

    protected override Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    ) => LiDaoPowerSystem.ActivateKuLiAsync(
        choiceContext,
        this
    );

    protected override void OnGuRankChanged()
    {
        base.OnGuRankChanged();
        RefreshRankValues();
    }

    internal static int ChancePerHardshipAtRank(int rank) => rank switch
    {
        <= 1 => 2,
        2 => 3,
        3 => 4,
        <= 5 => 5,
        6 => 6,
        7 => 7,
        8 => 8,
        _ => 10,
    };

    internal static bool CanRetryAllFailedAtRank(int rank, int hardship) =>
        rank >= 6 && hardship >= 2;

    internal static bool CanCreateDoubleShadowAtRank(int rank, int hardship) =>
        rank >= 8 && hardship >= 4;

    internal static bool CanDesperationBurstAtRank(int rank, int hardship) =>
        rank >= 9 && hardship >= 4;

    internal static decimal HardshipEffectBonusAtRank(
        int rank,
        int hardship
    )
    {
        if (rank == 3)
        {
            return hardship >= 3 ? 0.05m : 0m;
        }
        if (rank == 4)
        {
            return hardship >= 3 ? 0.08m : 0m;
        }

        decimal perHardship = rank switch
        {
            5 => 0.03m,
            6 => 0.04m,
            7 => 0.05m,
            8 => 0.06m,
            >= 9 => 0.07m,
            _ => 0m,
        };
        return hardship * perHardship;
    }

    private void RefreshRankValues() =>
        DynamicVars["ChancePerHardship"].BaseValue =
            ChancePerHardshipAtRank(GuRank);
}
