using System.Reflection;
using System.Threading;

using HarmonyLib;

using GuZhenRen.Characters;

using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRen.Cards;

/// <summary>
/// 蛊虫牌组达到容量上限后，获得新蛊虫不再直接失败或隐藏奖励。
/// 玩家选中新蛊虫后，必须从现有牌组中选择一张可合法替换的蛊虫。
/// 新牌先在“忽略待替换牌”的规则上下文中加入，成功后再移除旧牌，
/// 因此加入失败时不会提前损失原牌。
/// </summary>
internal static class GuDeckCapacityReplacementPatch
{
    private const string HarmonyId =
        Entry.ModId + ".GuDeckCapacityReplacement";

    private static readonly AsyncLocal<int> BypassDepth = new();

    private static readonly AsyncLocal<CardModel?>
        ActiveReplacementCard = new();

    private static bool _initialized;

    internal static CardModel? CardBeingReplaced =>
        ActiveReplacementCard.Value;

    internal static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        MethodInfo? addCard = AccessTools.Method(
            typeof(CardPileCmd),
            nameof(CardPileCmd.Add),
            [
                typeof(CardModel),
                typeof(PileType),
                typeof(CardPilePosition),
                typeof(AbstractModel),
                typeof(bool),
            ]
        );

        if (addCard == null)
        {
            throw new MissingMethodException(
                "蛊虫容量替换所需的 CardPileCmd.Add 重载不存在。"
            );
        }

        new Harmony(HarmonyId).Patch(
            addCard,
            prefix: new HarmonyMethod(
                typeof(GuDeckCapacityReplacementPatch),
                nameof(AddCardPrefix)
            )
        );

        _initialized = true;
    }

    internal static void Uninitialize()
    {
        try
        {
            new Harmony(HarmonyId).UnpatchAll(HarmonyId);
        }
        finally
        {
            BypassDepth.Value = 0;
            ActiveReplacementCard.Value = null;
            _initialized = false;
        }
    }

    private static bool AddCardPrefix(
        CardModel card,
        PileType newPileType,
        CardPilePosition position,
        AbstractModel? clonedBy,
        bool skipVisuals,
        ref Task<CardPileAddResult> __result
    )
    {
        if (BypassDepth.Value > 0 ||
            newPileType != PileType.Deck ||
            card is not IGuWormCard ||
            card.Pile != null ||
            card.Owner.Character is not GuZhenRenCharacter)
        {
            return true;
        }

        int existingGuCount =
            card.Owner.Deck.Cards.Count(existing =>
                existing is IGuWormCard
            );

        if (existingGuCount <
            GuZhenRenCardRules.GuWormDeckCapacity)
        {
            return true;
        }

        __result = ReplaceThenAdd(
            card,
            newPileType,
            position,
            clonedBy,
            skipVisuals
        );
        return false;
    }

    private static async Task<CardPileAddResult> ReplaceThenAdd(
        CardModel card,
        PileType newPileType,
        CardPilePosition position,
        AbstractModel? clonedBy,
        bool skipVisuals
    )
    {
        Player player = card.Owner;
        CardModel[] replaceableCards =
            player.Deck.Cards
                .Where(existing =>
                    GuZhenRenCardRules.CanReplaceGuWorm(
                        player,
                        existing,
                        card
                    )
                )
                .ToArray();

        if (replaceableCards.Length == 0)
        {
            Entry.Logger.Info(
                $"无法获得 {card.Id}：蛊虫容量已满，且没有可合法替换的蛊虫。"
            );
            return FailedResult(card, newPileType);
        }

        LocString prompt = new(
            "cards",
            "GU_ZHEN_REN_PERSONAL_CARD_REPLACE_GU_WORM.selectionScreenPrompt"
        );
        prompt.Add(
            "Capacity",
            GuZhenRenCardRules.GuWormDeckCapacity
        );
        prompt.Add("NewCard", card.Title);

        CardSelectorPrefs prefs = new(prompt, 1, 1)
        {
            Cancelable = false,
            RequireManualConfirmation = true,
        };

        CardModel? replacement =
            (
                await CardSelectCmd.FromDeckGeneric(
                    player,
                    prefs,
                    filter: existing =>
                        replaceableCards.Contains(existing),
                    sortingOrder: existing =>
                        existing is IGuRankProvider gu
                            ? gu.GuRank
                            : int.MaxValue
                )
            )
            .FirstOrDefault();

        if (replacement == null ||
            replacement.Pile?.Type != PileType.Deck)
        {
            return FailedResult(card, newPileType);
        }

        CardPileAddResult addResult;

        ActiveReplacementCard.Value = replacement;
        BypassDepth.Value++;

        try
        {
            addResult = await CardPileCmd.Add(
                card,
                newPileType,
                position,
                clonedBy,
                skipVisuals
            );
        }
        finally
        {
            BypassDepth.Value--;
            ActiveReplacementCard.Value = null;
        }

        if (!addResult.success)
        {
            return addResult;
        }

        try
        {
            await CardPileCmd.RemoveFromDeck(
                replacement,
                showPreview: false
            );
        }
        catch
        {
            // 极端情况下移除旧牌失败，撤销刚加入的新牌，避免容量永久超限。
            if (addResult.cardAdded.Pile?.Type == PileType.Deck)
            {
                await CardPileCmd.RemoveFromDeck(
                    addResult.cardAdded,
                    showPreview: false
                );
            }

            throw;
        }

        Entry.Logger.Info(
            $"蛊虫容量替换：移除 {replacement.Id}，获得 {addResult.cardAdded.Id}。"
        );

        return addResult;
    }

    private static CardPileAddResult FailedResult(
        CardModel card,
        PileType targetPile
    )
    {
        return new CardPileAddResult
        {
            cardAdded = card,
            success = false,
            oldPile = card.Pile,
            targetPile = targetPile,
            modifyingModels = null,
        };
    }
}
