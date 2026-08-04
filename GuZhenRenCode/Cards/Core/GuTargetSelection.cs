using GuZhenRen.Characters;
using GuZhenRen.Multiplayer;

using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards;

internal static class GuTargetSelection
{
    public static async Task<Creature?> SelectEnemy(
        PlayerChoiceContext choiceContext,
        Player player,
        CardModel sourceCard
    )
    {
        Creature[] enemies = GuZhenRenDeterminism.OrderCreatures(
            player.Creature.CombatState?.HittableEnemies ?? []
        ).ToArray();

        if (enemies.Length == 0)
        {
            return null;
        }

        if (enemies.Length == 1)
        {
            return enemies[0];
        }

        List<CardModel> choices = new(enemies.Length);
        for (int index = 0; index < enemies.Length; index++)
        {
            EnemyTargetChoice choice =
                (EnemyTargetChoice)ModelDb
                    .Card<EnemyTargetChoice>()
                    .ToMutable();
            choice.Owner = player;
            choice.TargetIndex = index;
            choice.TargetName = enemies[index].Name;
            choices.Add(choice);
        }

        LocString prompt = new(
            "cards",
            "GU_ZHEN_REN_CARD_ENEMY_TARGET_CHOICE.selectionScreenPrompt"
        );
        prompt.Add("SourceCard", sourceCard.Title);

        CardSelectorPrefs prefs = new(prompt, 1, 1)
        {
            Cancelable = false,
            PretendCardsCanBePlayed = true,
        };

        EnemyTargetChoice? selected =
            (
                await CardSelectCmd.FromSimpleGrid(
                    choiceContext,
                    choices,
                    player,
                    prefs
                )
            )
            .OfType<EnemyTargetChoice>()
            .FirstOrDefault();

        return selected != null &&
            selected.TargetIndex >= 0 &&
            selected.TargetIndex < enemies.Length
                ? enemies[selected.TargetIndex]
                : null;
    }
}

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class EnemyTargetChoice
    : ModCardTemplate,
      ICardRewardExcluded
{
    public int TargetIndex { get; set; } = -1;

    public string TargetName { get; set; } = string.Empty;

    public override string Title =>
        string.IsNullOrWhiteSpace(TargetName)
            ? base.Title
            : TargetName;

    public override CardPoolModel Pool =>
        ModelDb.CardPool<GuZhenRenCardPool>();

    public override CardAssetProfile AssetProfile =>
        global::GuZhenRen.Cards.CardImageCatalog.Create(GetType());

    public override bool CanBeGeneratedInCombat => false;

    public EnemyTargetChoice()
        : base(
            baseCost: 0,
            type: CardType.Skill,
            rarity: CardRarity.Token,
            target: TargetType.Self,
            showInCardLibrary: false
        )
    {
    }

    protected override bool IsPlayable => false;

    protected override void OnUpgrade()
    {
    }
}
