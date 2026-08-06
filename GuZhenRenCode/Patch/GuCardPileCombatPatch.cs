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
        bool UpgradeGroupingState,
        string EnchantmentId,
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
                    GetUpgradeGroupingState(card),
                    GetEnchantmentId(card),
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
    /// 普通实例字段通常已经携带正确转数；这里只在克隆桥丢失时修复，
    /// 并按卡牌 ID、普通牌升级状态和附魔分组，避免同名卡牌因洗牌
    /// 交换转数。真正蛊虫不使用 IsUpgraded 参与分组，因为该值由转数
    /// 派生，而战斗克隆可能在转数恢复前暂时呈现不同状态。
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
            (
                string CardId,
                bool UpgradeGroupingState,
                string EnchantmentId
            ),
            List<int>
        > ranksByCard = [];

        foreach (GuRankEntry entry in snapshot.Entries)
        {
            var key = (
                entry.CardId,
                entry.UpgradeGroupingState,
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
                GetUpgradeGroupingState(card),
                GetEnchantmentId(card)
            );
            if (!ranksByCard.TryGetValue(key, out List<int>? ranks) ||
                ranks.Count == 0)
            {
                continue;
            }

            // MutableClone 通常已经携带正确的转数桥字段。只要该转数
            // 确实存在于永久牌组的同卡、同升级、同附魔分组中，就原样
            // 保留；不能再按战斗牌堆顺序排队覆盖，因为牌堆会被洗牌，
            // 旧逻辑会让“华彩”看起来随机跳到其他同名蛊虫转数上。
            int currentRank = card.GuRank;
            int matchingRankIndex = ranks.IndexOf(currentRank);
            int sourceRank;
            if (matchingRankIndex >= 0)
            {
                sourceRank = currentRank;
                ranks.RemoveAt(matchingRankIndex);
            }
            else
            {
                // 仅对旧存档或确实丢失克隆桥字段的实例回退修复；
                // 回退范围仍限制在同卡、同普通牌升级状态、同附魔分组内。
                sourceRank = ranks[0];
                ranks.RemoveAt(0);
            }

            matchedCount++;

            if (currentRank != sourceRank)
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

    private static bool GetUpgradeGroupingState(
        AbstractGuZhenRenCard card
    )
    {
        // 六转仙蛊的 IsUpgraded 由 BaseGuRank 派生。转数恢复之前，
        // 永久牌和战斗克隆可能短暂返回不同值，所以蛊牌必须忽略该键。
        // 普通牌仍按真实升级状态分组，维持原有同名牌匹配精度。
        return card is not AbstractGuWormCard &&
            card.IsUpgraded;
    }

    private static string GetEnchantmentId(CardModel card)
    {
        return card.Enchantment?.Id.ToString() ?? string.Empty;
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
