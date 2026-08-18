using GuZhenRen.Characters;
using GuZhenRen.Combat;
using GuZhenRen.Powers.LiDao;

using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;

namespace GuZhenRen.Cards.LiDao;

/// <summary>
/// 我力蛊：监听力道虚影显化，把累计显化次数转化为力量。
/// 5转只统计自然显化；6转起计入全部实际显化（含群力额外显化）；
/// 7转起同回合三种不同力道虚影实际显化时额外获得力量；我力虚影也算一种。
/// 本蛊不属于兽力虚影蛊，不参与炼力、虚影容量或衍生牌系统。
/// </summary>
[RegisterCard(typeof(GuZhenRenGuCardPool))]
public sealed class WoLiGu : AbstractGuWormCard
{
    public override int MinimumAvailableGuRank => 5;

    public override int MaxGuRank => 7;

    public override int RecoveryDelayTurns => GuRank switch
    {
        <= 4 => 1,
        5 => 2,
        6 => 3,
        _ => 4,
    };

    public WoLiGu()
        : base(
            1,
            CardType.Power,
            CardRarity.Rare,
            TargetType.Self
        )
    {
        SetDao(Dao.LiDao);
        this.SecondaryCosts().Set(YuanQiSystem.ResourceId, 1);
    }

    protected override Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    ) => LiDaoPowerSystem.ActivateWoLiAsync(
        choiceContext,
        this
    );

    internal static int ManifestsPerStrengthAtRank(int rank) =>
        rank >= 6 ? 2 : 3;

    internal static int DistinctPhantomThreshold => 3;
}
