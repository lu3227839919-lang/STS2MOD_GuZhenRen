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
    /// RitsuLib SavedAttachedState 参与存档，但不保证随该克隆过程复制，
    /// 因此在蛊牌移入自定义牌堆前，按卡牌 ID、升级状态和重复出现顺序
    /// 把永久牌组转数重新写入战斗实例。
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

        Dictionary<(string CardId, bool IsUpgraded), Queue<int>>
            ranksByCard = [];

        foreach (GuRankEntry entry in snapshot.Entries)
        {
            var key = (entry.CardId, entry.IsUpgraded);
            if (!ranksByCard.TryGetValue(key, out Queue<int>? ranks))
            {
                ranks = new Queue<int>();
                ranksByCard.Add(key, ranks);
            }

            ranks.Enqueue(entry.Rank);
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
            var key = (card.Id.ToString(), card.IsUpgraded);
            if (!ranksByCard.TryGetValue(key, out Queue<int>? ranks) ||
                ranks.Count == 0)
            {
                continue;
            }

            int sourceRank = ranks.Dequeue();
            matchedCount++;

            if (card.GuRank != sourceRank)
            {
                card.InitializeGuRankFromSource(sourceRank);
                reconciledCount++;
            }

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
