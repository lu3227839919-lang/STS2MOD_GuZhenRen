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
/// 每回合前 N 次自然显化按概率使该虚影额外显化一次；
/// 额外显化不会再次触发群力（递归阻断），6转起计入实际显化。
/// 本蛊不属于兽力虚影蛊，不参与炼力、虚影容量或衍生牌系统。
/// </summary>
[RegisterCard(typeof(GuZhenRenGuCardPool))]
public sealed class QunLiGu : AbstractGuWormCard
{
    public override int YuanQiCost => 2;

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
        this.SecondaryCosts().Set(YuanQiSystem.ResourceId, 2);
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
        >= 7 => 45,
        6 => 35,
        _ => 25,
    };

    internal static int GroupTriggerLimitAtRank(int rank) =>
        rank >= 7 ? 2 : 1;
}
