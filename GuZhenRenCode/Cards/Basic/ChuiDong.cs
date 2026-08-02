using GuZhenRen.Cards;
using GuZhenRen.Cards.ImmortalEssence;
using GuZhenRen.Characters;
using GuZhenRen.Multiplayer;
using GuZhenRen.Patches;

using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
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
                card => card is IGuWormCard
                        && GuCardUsageRules.CanUse(card)
                        && ImmortalEssenceSystem
                            .CanPayForActivation(card)
            );

        if (selected is null)
        {
            return;
        }

        Creature? target = null;

        if (selected.TargetType == TargetType.AnyEnemy)
        {
            target =
                GuZhenRenDeterminism.OrderCreatures(
                    Owner.Creature.CombatState?.HittableEnemies ?? []
                )
                    .FirstOrDefault();

            if (target == null)
            {
                return;
            }
        }

        await CardCmd.AutoPlay(
            choiceContext,
            selected,
            target,
            skipCardPileVisuals: false
        );
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Ethereal);
    }
}
