using GuZhenRen.Characters;
using GuZhenRen.Combat;
using GuZhenRen.Powers.LiDao;

using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;

namespace GuZhenRen.Cards.LiDao;

/// <summary>
/// 群力蛊：兽力虚影自然显化后的虚影发动机。
/// 每次自然显化都开启一条独立连锁，按概率使同一虚影连续额外显化；
/// 单次连锁最多额外显化 1/2/3 次，且不会递归开启新连锁，
/// 6转起每次额外显化均计入实际显化。
/// 本蛊不属于兽力虚影蛊，不参与炼力、虚影容量或衍生牌系统。
/// </summary>
[RegisterCard(typeof(GuZhenRenGuCardPool))]
public sealed class QunLiGu : AbstractGuWormCard
{
    public override int YuanQiCost => 1;

    public override int MinimumAvailableGuRank => 5;

    public override int MaxGuRank => 7;

    public override int RecoveryDelayTurns => GuRank switch
    {
        <= 4 => 1,
        5 => 2,
        6 => 3,
        _ => 4,
    };

    public QunLiGu()
        : base(
            1,
            CardType.Power,
            CardRarity.Uncommon,
            TargetType.Self
        )
    {
        SetDao(Dao.LiDao);
        this.SecondaryCosts().Set(YuanQiSystem.ResourceId, YuanQiCost);
    }

    protected override Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    ) => LiDaoPowerSystem.ActivateQunLiAsync(
        choiceContext,
        this
    );

    internal static int GroupChanceAtRank(int rank) => rank switch
    {
        >= 7 => 40,
        6 => 30,
        _ => 20,
    };

    internal static int GroupRepeatLimitAtRank(int rank) => rank switch
    {
        >= 7 => 3,
        6 => 2,
        _ => 1,
    };

    internal static bool ExtraManifestCountsAsActualAtRank(int rank) =>
        rank >= 6;
}
