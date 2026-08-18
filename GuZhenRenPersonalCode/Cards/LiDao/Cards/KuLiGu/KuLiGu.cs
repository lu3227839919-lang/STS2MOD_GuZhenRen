using GuZhenRen.Characters;
using GuZhenRen.Combat;
using GuZhenRen.Powers.LiDao;

using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;

using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;

namespace GuZhenRen.Cards.LiDao;

/// <summary>
/// 苦力蛊：伤势越重，攻击牌结算后追加的苦力伤害越高。
/// 本蛊不属于兽力虚影蛊，不参与炼力、虚影容量或衍生牌系统。
/// </summary>
[RegisterCard(typeof(GuZhenRenGuCardPool))]
public sealed class KuLiGu : AbstractGuWormCard
{
    public override int YuanQiCost => 2;

    public override int MinimumAvailableGuRank => 3;

    public override int MaxGuRank => 7;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        base.CanonicalKeywords
            .Append(GuZhenRenKeywords.ShangShi)
            .Distinct();

    public override int RecoveryDelayTurns => GuRank switch
    {
        <= 4 => 1,
        5 => 2,
        6 => 3,
        _ => 4,
    };

    public KuLiGu()
        : base(
            1,
            CardType.Power,
            CardRarity.Uncommon,
            TargetType.Self
        )
    {
        SetDao(Dao.LiDao);
        this.SecondaryCosts().Set(YuanQiSystem.ResourceId, 2);
    }

    protected override void AddExtraArgsToDescription(
        LocString description
    )
    {
        base.AddExtraArgsToDescription(description);
        description.Add("LightDamage", ExtraDamageAtRank(GuRank, 1));
        description.Add("HeavyDamage", ExtraDamageAtRank(GuRank, 2));
        description.Add("CriticalDamage", ExtraDamageAtRank(GuRank, 3));
    }

    protected override Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    ) => LiDaoPowerSystem.ActivateKuLiAsync(
        choiceContext,
        this
    );

    internal static int ExtraDamageAtRank(int rank, int injuryTier) =>
        rank switch
        {
            <= 4 => injuryTier * 2,
            <= 6 => injuryTier switch
            {
                1 => 2,
                2 => 5,
                3 => 9,
                _ => 0,
            },
            _ => injuryTier switch
            {
                1 => 3,
                2 => 6,
                3 => 10,
                _ => 0,
            },
        };

    internal static int StrengthAtThreshold(int rank, int injuryTier) =>
        rank switch
        {
            >= 7 when injuryTier == 3 => 2,
            >= 6 => 1,
            _ => 0,
        };
}
