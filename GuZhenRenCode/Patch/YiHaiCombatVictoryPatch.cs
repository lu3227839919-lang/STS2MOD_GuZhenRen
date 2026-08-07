using System.Reflection;

using GuZhenRen.Cards.XueDao;

using HarmonyLib;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace GuZhenRen.Patches;

/// <summary>
/// 战斗胜利后把未消耗的遗骸（YiHai）移入永久牌组。
///
/// 原版 Hook.AfterCombatEnd 在胜利结算流程（CombatManager 宿主端）中
/// 于“清空战斗牌堆”（Player.AfterCombatEnd）之前调用，此时战斗卡牌
/// 实例仍然有效；AfterCombatVictory 则在清空之后触发，届时遗骸已被
/// 移出战斗牌堆，无法再收集。CardPileCmd 会把牌堆变更同步给多人
/// 客户端。遗骸从手牌/抽牌堆/弃牌堆收集（已进入消耗堆的遗骸不算，
/// 永久牌组卡的战斗克隆体 DeckVersion != null 也不算），并按永久
/// 牌堆上限（XueDaoCardSystem.MaxPersistentRemains）截断；未使用的
/// 遗骸因此保留到永久牌组，与本地化提示一致。
/// </summary>
internal static class YiHaiCombatVictoryPatch
{
    private const string HarmonyId =
        Entry.ModId + ".YiHaiCombatVictory";

    private static bool _initialized;

    internal static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        MethodInfo original =
            AccessTools.Method(
                typeof(Hook),
                nameof(Hook.AfterCombatEnd)
            )
            ?? throw new MissingMethodException(
                typeof(Hook).FullName,
                nameof(Hook.AfterCombatEnd)
            );

        MethodInfo postfix =
            AccessTools.Method(
                typeof(YiHaiCombatVictoryPatch),
                nameof(AfterCombatEndPostfix)
            )
            ?? throw new MissingMethodException(
                typeof(YiHaiCombatVictoryPatch).FullName,
                nameof(AfterCombatEndPostfix)
            );

        new Harmony(HarmonyId).Patch(
            original,
            postfix: new HarmonyMethod(postfix)
        );

        _initialized = true;
    }

    internal static void Uninitialize()
    {
        try
        {
            new Harmony(HarmonyId)
                .UnpatchAll(HarmonyId);
        }
        finally
        {
            _initialized = false;
        }
    }

    private static void AfterCombatEndPostfix(
        ref Task __result,
        IRunState runState,
        CombatRoom room
    )
    {
        Entry.Logger.Info(
            "[遗骸保留] AfterCombatEnd postfix 触发。"
        );
        __result = AwaitPersistRemainsAsync(
            __result,
            runState
        );
    }

    private static async Task AwaitPersistRemainsAsync(
        Task original,
        IRunState runState
    )
    {
        await original;

        foreach (Player player in runState.Players)
        {
            await PersistRemainsForPlayer(player);
        }
    }

    private static async Task PersistRemainsForPlayer(Player player)
    {
        int handYiHai = PileType.Hand
            .GetPile(player)
            .Cards
            .Count(static card => card is YiHai);
        int drawYiHai = PileType.Draw
            .GetPile(player)
            .Cards
            .Count(static card => card is YiHai);
        int discardYiHai = PileType.Discard
            .GetPile(player)
            .Cards
            .Count(static card => card is YiHai);
        int existing = player.Deck.Cards.Count(
            static card => card is YiHai
        );

        Entry.Logger.Info(
            $"[遗骸保留] {player.NetId} 检查：永久牌组 {existing}，" +
            $"手牌 {handYiHai}，抽牌堆 {drawYiHai}，弃牌堆 {discardYiHai}。"
        );

        // 永久牌组中遗骸最多 4 张：已达上限时本场战斗的遗骸不再保留。
        int slots = Math.Max(
            0,
            XueDaoCardSystem.MaxPersistentRemains - existing
        );
        if (slots <= 0)
        {
            Entry.Logger.Info(
                $"[遗骸保留] {player.NetId} 永久牌组遗骸已达上限，跳过。"
            );
            return;
        }

        // 未使用的遗骸可能留在手牌（保留）或弃牌堆/抽牌堆；
        // 已消耗（进入 Exhaust 堆）的遗骸不在此列。按 Id 稳定排序，
        // 保证多人端以相同顺序加入牌组。
        YiHai[] remains = new[]
            {
                PileType.Hand.GetPile(player),
                PileType.Draw.GetPile(player),
                PileType.Discard.GetPile(player),
            }
            .SelectMany(static pile => pile.Cards)
            .OfType<YiHai>()
            // 永久牌组卡的战斗克隆体（DeckVersion != null）不计入：
            // 其原件已常驻永久牌组，战斗结束克隆体由原版销毁即可，
            // 避免与原件重复保留。
            .Where(static card => card.DeckVersion == null)
            .Distinct()
            .OrderBy(
                static card => card.Id.ToString(),
                StringComparer.Ordinal
            )
            .Take(slots)
            .ToArray();

        if (remains.Length == 0)
        {
            return;
        }

        // 战斗卡只登记在 CombatState（CombatState.CreateCard 不写入
        // RunState._allCards）；CardPileCmd.Add 到永久牌组要求卡已登记
        // 到 RunState。先脱离战斗牌堆并清 Owner，再补登记后入牌组。
        foreach (YiHai card in remains)
        {
            card.RemoveFromCurrentPile();
            card.Owner = null;
            player.RunState.AddCard(card, player);
        }

        await CardPileCmd.Add(
            remains,
            PileType.Deck,
            CardPilePosition.Bottom,
            clonedBy: null,
            skipVisuals: true
        );

        Entry.Logger.Info(
            $"[遗骸保留] {player.NetId} 战斗胜利后保留 " +
            $"{remains.Length} 张遗骸至永久牌组。"
        );
    }
}
