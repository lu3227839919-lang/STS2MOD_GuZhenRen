using GuZhenRen.Cards;
using GuZhenRen.Characters;
using GuZhenRen.Patches;

using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.Basic;

[RegisterCard(typeof(GuZhenRenCardPool))]
[RegisterCharacterStarterCard(typeof(GuZhenRenCharacter), 4)]
public sealed class ChuiDong
    : ModCardTemplate, ICardRewardExcluded
{
    public override CardAssetProfile AssetProfile =>
        new(
            PortraitPath:
                $"{Entry.ResPath}/images/cards/ChuiDong.png"
        );

    public ChuiDong()
        : base(
            baseCost: 1,
            type: CardType.Skill,
            rarity: CardRarity.Basic,
            target: TargetType.Self
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

        if (Owner.PlayerCombatState == null)
        {
            return;
        }

        CardPile guPile =
            GuCardPileSystem.PileType.GetPile(Owner);

        if (guPile.Cards.Count == 0)
        {
            return;
        }

        CardModel? selected =
            await GuCardStackSelectionPatch.SelectOne(
                choiceContext,
                guPile,
                Owner,
                new CardSelectorPrefs(
                    SelectionScreenPrompt,
                    1,
                    1
                ),
                GuCardUsageRules.CanActivate
            );

        if (selected is null)
        {
            return;
        }

        Creature? target = null;

        if (selected.TargetType == TargetType.AnyEnemy)
        {
            target = await GuTargetSelection.SelectEnemy(
                choiceContext,
                Owner,
                selected
            );

            if (target == null)
            {
                return;
            }
        }

        if (!await GuCardUsageRules
                .PrepareActivationPayment(selected))
        {
            return;
        }

        try
        {
            await CardCmd.AutoPlay(
                choiceContext,
                selected,
                target,
                skipCardPileVisuals: false
            );
        }
        finally
        {
            // 若 AutoPlay 在进入 BeforeCardPlayed 前被取消，不能让预付标记
            // 泄漏到下一次催动并形成免费支付。
            GuCardUsageRules.ClearPreparedActivationPayment(selected);
        }
    }

    protected override void OnUpgrade()
    {
        // 升级应提高可用性，而不是通过“虚无”永久减少后续检索次数。
        EnergyCost.UpgradeBy(-1);
    }
}
