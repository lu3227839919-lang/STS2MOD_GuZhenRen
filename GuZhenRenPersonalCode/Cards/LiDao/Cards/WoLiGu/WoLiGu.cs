using GuZhenRen.Characters;
using GuZhenRen.Powers.LiDao;

using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Interop.AutoRegistration;

namespace GuZhenRen.Cards.LiDao;

/// <summary>
/// 我力蛊：本场战斗持续监听力道虚影实际触发，并把每两次触发
/// 转化为原版力量。它不使用练力/催动次数循环。
/// </summary>
[RegisterCard(typeof(GuZhenRenGuCardPool))]
public sealed class WoLiGu : AbstractGuWormCard
{
    public override int YuanQiCost => 0;

    public override int MaxUses => 1;

    public override int RecoveryDelayTurns => 0;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("PhantomTriggersPerStrength", 2m)];

    public WoLiGu() : base(0, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
        SetDao(Dao.LiDao);
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        await LiDaoPowerSystem.ActivateWoLiAsync(
            choiceContext,
            this
        );
    }
}
