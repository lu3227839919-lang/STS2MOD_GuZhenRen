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

    protected override bool IsPlayable
    {
        get
        {
            if (IsCanonical)
            {
                return false;
            }

            Player owner = Owner;

            return base.IsPlayable &&
                owner.PlayerCombatState != null &&
                !GuActivationModeSystem.IsActiveFor(owner) &&
                GuCardPileSystem.PileType
                    .GetPile(owner)
                    .Cards
                    .Any(GuCardUsageRules.CanActivate);
        }
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        ArgumentNullException.ThrowIfNull(choiceContext);
        ArgumentNullException.ThrowIfNull(cardPlay);

        GuActivationModeSystem.Begin(Owner);
        await Task.CompletedTask;
    }

    protected override void OnUpgrade()
    {
        // 升级应提高可用性，而不是通过“虚无”永久减少后续检索次数。
        EnergyCost.UpgradeBy(-1);
    }
}
