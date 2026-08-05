using GuZhenRen.Cards;
using GuZhenRen.Characters;

using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.Basic;

[RegisterCard(typeof(GuZhenRenCardPool))]
[RegisterCharacterStarterCard(typeof(GuZhenRenCharacter), 4)]
public sealed class ChuiDong
    : ModCardTemplate, ICardRewardExcluded
{
    public override CardAssetProfile AssetProfile =>
        global::GuZhenRen.Cards.CardImageCatalog.Create(GetType());

    public ChuiDong()
        : base(
            baseCost: 1,
            type: CardType.Skill,
            rarity: CardRarity.Basic,
            target: TargetType.None
        )
    {
    }

    protected override bool IsPlayable =>
        !IsCanonical &&
        GuActivationModeSystem.IsAutoPlayingActivator(this);

    protected override Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        ArgumentNullException.ThrowIfNull(choiceContext);
        ArgumentNullException.ThrowIfNull(cardPlay);

        // “催动”现在是蛊牌的自动支付载体。选择蛊牌并确认目标后，
        // GuActivationModeSystem 会自动打出一张手牌中的催动。
        return Task.CompletedTask;
    }

    protected override void OnUpgrade()
    {
        // 升级应提高可用性，而不是通过“虚无”永久减少后续检索次数。
        EnergyCost.UpgradeBy(-1);
    }
}
