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

    internal static int ImmediateHealAtRank(int rank, int forceBase) =>
        rank switch
        {
            <= 1 => 1,
            2 => 1 + forceBase / 2,
            >= 9 => 2,
            _ => 0,
        };

    internal static int LifeForceCapAtRank(int rank) => rank >= 6 ? 2 : 0;

    internal static bool HealsAtTurnEndAtRank(int rank) => rank >= 3;

    internal static int ForceBaseBonusAtRank(int rank, int forceBase) =>
        rank >= 5 && forceBase >= 3 ? 2 : 0;

    internal static int LifeForceBonusAtRank(int rank, int lifeForce) =>
        rank >= 6 ? lifeForce : 0;

    internal static int ManifestationBonusAtRank(
        int rank,
        int manifestedKinds,
        int hardship
    ) => rank >= 8 && manifestedKinds >= 3
        ? hardship >= 3 ? 5 : 3
        : 0;

    internal static decimal LowHealthMultiplierAtRank(
        int rank,
        int currentHp,
        int maxHp
    ) => rank >= 9 && currentHp * 100 < maxHp * 30 ? 1.5m : 1m;

    internal static int OverflowBlockAtRank(int rank, int overflow) =>
        rank >= 7 && overflow >= 2 ? Math.Min(6, overflow / 2) : 0;

    private void RefreshRankValues() => DynamicVars.Heal.BaseValue =
        HealingCapAtRank(GuRank);

    internal static int HealingCapAtRank(int rank) => rank switch
    {
        <= 1 => 1, 2 => 2, 3 => 3, 4 => 4, 5 => 5,
        6 => 6, 7 => 7, 8 => 9, _ => 12,
    };

}
