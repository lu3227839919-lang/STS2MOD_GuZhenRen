using System.Reflection;

using GuZhenRen.Cards;

using HarmonyLib;

using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;

namespace GuZhenRen.Patches;

/// <summary>
/// Moves every Gu card cloned into a combat draw pile into the dedicated Gu
/// pile before the first draw.  This keeps Gu cards out of normal hand draws.
/// </summary>
internal static class GuCardPileCombatPatch
{
    private const string HarmonyId =
        Entry.ModId + ".GuCardPileCombat";

    private static bool _initialized;

    private sealed class GuRankSnapshot
    {
        public required IReadOnlyList<GuRankEntry> Entries { get; init; }
    }

    private readonly record struct GuRankEntry(
        string CardId,
        bool IsUpgraded,
        string? EnchantmentId,
        int Rank
    );

    internal static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        MethodInfo? populateCombatState =
            AccessTools.DeclaredMethod(
                typeof(Player),
                nameof(Player.PopulateCombatState),
                [typeof(Rng), typeof(CombatState)]
            );

        if (populateCombatState == null)
        {
            throw new MissingMethodException(
                "Player.PopulateCombatState was not found."
            );
        }

        MethodInfo? drawInternal =
            AccessTools.DeclaredMethod(
                typeof(CardPileCmd),
                "DrawInternal",
                [
                    typeof(PlayerChoiceContext),
                    typeof(decimal),
                    typeof(Player),
                    typeof(bool),
                ]
            );

        if (drawInternal == null)
        {
            throw new MissingMethodException(
                "CardPileCmd.DrawInternal was not found."
            );
        }

        if (drawInternal.ReturnType !=
            typeof(Task<IEnumerable<CardModel>>))
        {
            throw new MissingMethodException(
                "CardPileCmd.DrawInternal has an unexpected return type."
            );
        }

        Harmony harmony = new(HarmonyId);
        harmony.Patch(
            populateCombatState,
            prefix: new HarmonyMethod(
                typeof(GuCardPileCombatPatch),
                nameof(PopulateCombatStatePrefix)
            ),
            postfix: new HarmonyMethod(
                typeof(GuCardPileCombatPatch),
                nameof(PopulateCombatStatePostfix)
            )
        );

        harmony.Patch(
            drawInternal,
            prefix: new HarmonyMethod(
                typeof(GuCardPileCombatPatch),
                nameof(DrawInternalPrefix)
            ),
            postfix: new HarmonyMethod(
                typeof(GuCardPileCombatPatch),
                nameof(DrawInternalPostfix)
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
            _initialized = false;
        }
    }

    private static void PopulateCombatStatePrefix(
        Player __instance,
        out GuRankSnapshot __state
    )
    {
        __state = new GuRankSnapshot
        {
            Entries = __instance.Deck.Cards
                .OfType<AbstractGuZhenRenCard>()
                .Select(card => new GuRankEntry(
                    card.Id.ToString(),
                    card.IsUpgraded,
                    card.Enchantment?.Id.ToString(),
                    card.GuRank
                ))
                .ToArray(),
        };
    }

    private static void PopulateCombatStatePostfix(
        Player __instance,
        GuRankSnapshot __state
    )
    {
        ReconcileCombatCardRanks(__instance, __state);
        GuCardPileSystem.InitializeGuCardsForCombat(__instance);
    }

    /// <summary>
    /// 原版 PopulateCombatState 会把永久牌组复制成战斗实例。
    /// RitsuLib SavedAttachedState 参与存档，但不保证随该克隆过程复制。
    /// 因此在蛊牌移入自定义牌堆前，优先保留战斗克隆已有的正确转数；
    /// 真正丢失转数时，再按卡牌 ID、升级状态和附魔进行回退校准。
    /// </summary>
    private static void ReconcileCombatCardRanks(
        Player owner,
        GuRankSnapshot snapshot
    )
    {
        if (snapshot.Entries.Count == 0)
        {
            return;
        }

        Dictionary<
            (string CardId, bool IsUpgraded, string? EnchantmentId),
            List<int>
        > ranksByCard = [];

        foreach (GuRankEntry entry in snapshot.Entries)
        {
            var key = (
                entry.CardId,
                entry.IsUpgraded,
                entry.EnchantmentId
            );
            if (!ranksByCard.TryGetValue(key, out List<int>? ranks))
            {
                ranks = [];
                ranksByCard.Add(key, ranks);
            }

            ranks.Add(entry.Rank);
        }

        CardModel[] combatCards =
        [
            .. PileType.Draw.GetPile(owner).Cards,
            .. PileType.Discard.GetPile(owner).Cards,
            .. PileType.Hand.GetPile(owner).Cards,
        ];

        int matchedCount = 0;
        int reconciledCount = 0;
        List<string> elevatedCards = [];

        foreach (AbstractGuZhenRenCard card in combatCards
                     .OfType<AbstractGuZhenRenCard>())
        {
            var key = (
                card.Id.ToString(),
                card.IsUpgraded,
                card.Enchantment?.Id.ToString()
            );
            if (!ranksByCard.TryGetValue(key, out List<int>? ranks) ||
                ranks.Count == 0)
            {
                continue;
            }

            // MutableClone normally preserves the ordinary Gu-rank fields.
            // Keep that exact per-instance rank whenever it exists in the
            // source multiset instead of replacing it according to shuffled
            // combat-pile order.  This preserves the association between a
            // specific enchanted card and its rank.  Only cards whose rank
            // was genuinely lost fall back to the source order.
            int sourceRank;
            int preservedRankIndex = ranks.IndexOf(card.GuRank);
            if (preservedRankIndex >= 0)
            {
                sourceRank = card.GuRank;
                ranks.RemoveAt(preservedRankIndex);
            }
            else
            {
                sourceRank = ranks[0];
                ranks.RemoveAt(0);
                card.InitializeGuRankFromSource(sourceRank);
                reconciledCount++;
            }

            matchedCount++;

            if (sourceRank > AbstractGuZhenRenCard.MinimumGuRank)
            {
                elevatedCards.Add($"{card.Id}={sourceRank}转");
            }
        }

        if (reconciledCount > 0 || elevatedCards.Count > 0)
        {
            string detail = elevatedCards.Count > 0
                ? $" 高转卡牌：{string.Join(", ", elevatedCards)}。"
                : string.Empty;

            Entry.Logger.Info(
                $"[蛊虫转数] 战斗初始化已校验 {matchedCount} 张卡牌，" +
                $"修正 {reconciledCount} 张。" +
                detail
            );
        }
    }

    private static void DrawInternalPrefix(
        Player player,
        bool fromHandDraw,
        out Task? __state
    )
    {
        GuCardPileSystem.MoveStrayGuCardsToVillage(player);
        __state = GuCardPileSystem.BeginOpeningGuEntry(
            player,
            fromHandDraw
        );
    }

    private static void DrawInternalPostfix(
        Task? __state,
        ref Task<IEnumerable<CardModel>> __result
    )
    {
        if (__state == null)
        {
            return;
        }

        __result = AwaitDrawAndGuEntryAsync(__result, __state);
    }

    private static async Task<IEnumerable<CardModel>>
        AwaitDrawAndGuEntryAsync(
            Task<IEnumerable<CardModel>> drawTask,
            Task guEntryTask
        )
    {
        await Task.WhenAll(drawTask, guEntryTask);
        return await drawTask;
    }
}
