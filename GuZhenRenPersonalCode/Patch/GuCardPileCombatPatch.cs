using System.Reflection;

using GuZhenRen.Cards;
using GuZhenRen.Cards.LiDao;
using GuZhenRen.Cards.ZhouDao;
using GuZhenRen.Multiplayer;

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
/// pile before the first draw. Companion cards are generated into the native
/// draw pile before the native opening draw, with their order randomized
/// together with the existing cards.
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

        MethodInfo? startCombat =
            AccessTools.DeclaredMethod(
                typeof(NetCombatCardDb),
                nameof(NetCombatCardDb.StartCombat),
                [typeof(IReadOnlyList<Player>)]
            );

        if (startCombat == null)
        {
            throw new MissingMethodException(
                "NetCombatCardDb.StartCombat was not found."
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
            startCombat,
            prefix: new HarmonyMethod(
                typeof(GuCardPileCombatPatch),
                nameof(NetCombatCardDbStartCombatPrefix)
            ),
            postfix: new HarmonyMethod(
                typeof(GuCardPileCombatPatch),
                nameof(NetCombatCardDbStartCombatPostfix)
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
        int removedCompanionCount =
            RemoveLegacyPermanentCompanions(__instance);
        if (removedCompanionCount > 0)
        {
            Entry.Logger.Info(
                $"[伴生牌迁移] 已从永久牌组清理 " +
                $"{removedCompanionCount} 张旧版伴生牌；" +
                "本场及后续战斗将改为战斗内生成。"
            );
        }

        // 修复旧存档以及升转发生在不同重复实例上的多人状态：战斗克隆
        // 前先把同名蛊牌的转数多重集固定到原生 NetDeckCard 槽位。
        int canonicalizedCount =
            GuZhenRenDeterminism.CanonicalizeDeckGuRanks(__instance);
        if (canonicalizedCount > 0)
        {
            Entry.Logger.Info(
                $"[蛊虫转数] 战斗前已将 {canonicalizedCount} 张同名蛊牌" +
                "的转数规范到稳定牌组槽位。"
            );
        }

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

    /// <summary>
    /// 旧版本会把伴生牌写入永久牌组。战斗克隆前执行一次确定性迁移，
    /// 防止旧牌与新生成牌重复，并让旧存档也遵守“伴生牌仅存在于战斗”
    /// 的新规则。
    /// </summary>
    private static int RemoveLegacyPermanentCompanions(Player player)
    {
        CardModel[] legacyCompanions = player.Deck.Cards
            .Where(static card =>
                card is ILiDaoCompanionCard or IZhouDaoCompanionCard)
            .ToArray();

        if (legacyCompanions.Length == 0)
        {
            return 0;
        }

        foreach (CardModel companion in legacyCompanions)
        {
            player.Deck.RemoveInternal(companion, silent: true);
            player.RunState.RemoveCard(companion);
        }

        player.Deck.InvokeContentsChanged();
        return legacyCompanions.Length;
    }

    private static void PopulateCombatStatePostfix(
        Player __instance,
        GuRankSnapshot __state
    )
    {
        ReconcileCombatCardRanks(__instance, __state);
    }

    /// <summary>
    /// 在原生网络编号建立与首次抽牌之前，把伴生牌生成到抽牌堆。
    /// 生成完成后会重新随机整副抽牌堆，避免现有牌总是固定先于伴生牌。
    /// </summary>
    private static void NetCombatCardDbStartCombatPrefix(
        IReadOnlyList<Player> players
    )
    {
        foreach (Player player in players)
        {
            int liDaoCount = CompanionCardSystem.GenerateForCombat(
                player,
                CompanionCardSystem.CompanionPairingMode.OnePerSourceCard,
                static card =>
                    card is ILiDaoBeastGuCard beastGu
                        ? beastGu.CompanionCardType
                        : null,
                static card => card is ILiDaoCompanionCard,
                "力道",
                LiDaoBeastTrainingSystem.AllowCompanionTraining
            );
            int zhouDaoCount = CompanionCardSystem.GenerateForCombat(
                player,
                CompanionCardSystem.CompanionPairingMode.OnePerSourceType,
                static card =>
                    card is IZhouDaoCompanionGuCard zhouDao
                        ? zhouDao.CompanionCardType
                        : null,
                static card => card is IZhouDaoCompanionCard,
                "宙道"
            );
            int generatedCount = liDaoCount + zhouDaoCount;

            if (player.PlayerCombatState is { }
                && player.Creature.CombatState is CombatState combatState)
            {
                CardPile drawPile = PileType.Draw.GetPile(player);
                bool hasCompanionInDrawPile = drawPile.Cards.Any(
                    static card =>
                        card is ILiDaoCompanionCard or IZhouDaoCompanionCard
                );

                if (generatedCount > 0 || hasCompanionInDrawPile)
                {
                    drawPile.RandomizeOrderInternal(
                        player,
                        player.RunState.Rng.Shuffle,
                        combatState
                    );
                }

                if (generatedCount > 0)
                {
                    Entry.Logger.Info(
                        $"[伴生牌入场] 首次抽牌前已在抽牌堆生成 " +
                        $"{generatedCount} 张伴生牌（力道 {liDaoCount}，" +
                        $"宙道 {zhouDaoCount}），并与现有牌重新混洗。"
                    );
                }
            }
        }
    }

    /// <summary>
    /// 必须等原生 NetCombatCardDb 为全部战斗克隆（含刚生成的伴生牌）
    /// 建立编号后，再规范化重复蛊牌并搬入自定义牌堆。旧时序在编号
    /// 建立前就按无效的 uint.MaxValue 排序，会让两端携带不同转数。
    /// </summary>
    private static void NetCombatCardDbStartCombatPostfix(
        IReadOnlyList<Player> players
    )
    {
        foreach (Player player in players)
        {
            int canonicalizedCount =
                GuZhenRenDeterminism.CanonicalizeCombatGuRanks(player);
            if (canonicalizedCount > 0)
            {
                Entry.Logger.Info(
                    $"[蛊虫转数] 网络编号建立后已将 " +
                    $"{canonicalizedCount} 张同名战斗蛊牌的转数" +
                    "规范到稳定网络实例。"
                );
            }

            GuCardPileSystem.InitializeGuCardsForCombat(player);
        }
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

            // Even when the saved rank is already correct, a card created by
            // network deserialization may still carry rank-one DynamicVars
            // cloned from the canonical model. Always rebuild derived values
            // before the card can be activated.
            card.RefreshGuRankDerivedState();

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
        Player player
    )
    {
        GuCardPileSystem.MoveStrayGuCardsToVillage(player);
    }

    private static void DrawInternalPostfix(
        Player player,
        bool fromHandDraw,
        ref Task<IEnumerable<CardModel>> __result
    )
    {
        if (!fromHandDraw ||
            player.PlayerCombatState?.TurnNumber != 1)
        {
            return;
        }

        __result = AwaitDrawThenGuEntryAsync(
            __result,
            player,
            fromHandDraw
        );
    }

    private static async Task<IEnumerable<CardModel>>
        AwaitDrawThenGuEntryAsync(
            Task<IEnumerable<CardModel>> drawTask,
            Player player,
            bool fromHandDraw
        )
    {
        IEnumerable<CardModel> drawnCards = await drawTask;
        Task? guEntryTask = GuCardPileSystem.BeginOpeningGuEntry(
            player,
            fromHandDraw
        );
        if (guEntryTask != null)
        {
            await guEntryTask;
        }

        return drawnCards;
    }
}
