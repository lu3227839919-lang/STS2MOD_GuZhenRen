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
/// 自力更生蛊：标记下一张攻击牌，并按其对主目标实际造成的生命
/// 伤害回复生命。本蛊不参与炼力、虚影容量或衍生牌系统。
/// </summary>
[RegisterCard(typeof(GuZhenRenGuCardPool))]
public sealed class ZiLiGengShengGu : AbstractGuWormCard
{
    public override int MinimumAvailableGuRank => 3;

    public override int MaxGuRank => 7;

    public override int RecoveryDelayTurns => GuRank switch
    {
        <= 4 => 1,
        5 => 2,
        6 => 3,
        _ => 4,
    };

    public ZiLiGengShengGu()
        : base(
            1,
            CardType.Power,
            CardRarity.Uncommon,
            TargetType.Self
        )
    {
        SetDao(Dao.LiDao);
        this.SecondaryCosts().Set(YuanQiSystem.ResourceId, 1);
    }

    protected override void AddExtraArgsToDescription(
        LocString description
    )
    {
        base.AddExtraArgsToDescription(description);
        description.Add(
            "HealingPercent",
            (int)(HealingRatioAtRank(GuRank) * 100m)
        );
        description.Add("HealingCap", HealingCapAtRank(GuRank, false));
        description.Add("KillHealingCap", HealingCapAtRank(GuRank, true));
        description.Add("HasKillBonus", GuRank >= 7 ? 1 : 0);
    }

    protected override Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    ) => LiDaoPowerSystem.ActivateZiLiAsync(
        choiceContext,
        this
    );

    internal static decimal HealingRatioAtRank(int rank) => rank switch
    {
        <= 3 => 0.25m,
        4 => 0.30m,
        5 => 0.35m,
        6 => 0.50m,
        _ => 0.60m,
    };

    internal static int HealingCapAtRank(int rank, bool killedTarget) =>
        rank switch
        {
            <= 3 => 4,
            4 => 6,
            5 => 8,
            6 => 12,
            _ => killedTarget ? 20 : 16,
        };

    internal static bool CountsAttachedLiDaoDamageAtRank(int rank) =>
        rank >= 6;
}
