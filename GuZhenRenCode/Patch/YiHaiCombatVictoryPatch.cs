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
/// 原版 Hook.AfterCombatVictory 在战斗胜利结算时（CombatManager 宿主
/// 流程）调用，此时战斗卡牌实例仍然有效，且每局只在模拟端执行一次，
/// CardPileCmd 会把牌堆变更同步给多人客户端。遗骸从手牌/抽牌堆/弃牌堆
/// 收集（已进入消耗堆的遗骸不算），并按永久牌堆上限
/// （XueDaoCardSystem.MaxPersistentRemains）截断；未使用的遗骸因此
/// 保留到永久牌组，与本地化提示一致。
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
                nameof(Hook.AfterCombatVictory)
            )
            ?? throw new MissingMethodException(
                typeof(Hook).FullName,
                nameof(Hook.AfterCombatVictory)
            );

        MethodInfo postfix =
            AccessTools.Method(
                typeof(YiHaiCombatVictoryPatch),
                nameof(AfterCombatVictoryPostfix)
            )
            ?? throw new MissingMethodException(
                typeof(YiHaiCombatVictoryPatch).FullName,
                nameof(AfterCombatVictoryPostfix)
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

    private static async Task AfterCombatVictoryPostfix(
        IRunState runState,
        CombatRoom room
    )
    {
        foreach (Player player in runState.Players)
        {
            await PersistRemainsForPlayer(player);
        }
    }

    private static async Task PersistRemainsForPlayer(Player player)
    {
        // 永久牌组中遗骸最多 4 张：已达上限时本场战斗的遗骸不再保留。
        int existing = player.Deck.Cards.Count(
            static card => card is YiHai
        );
        int slots = Math.Max(
            0,
            XueDaoCardSystem.MaxPersistentRemains - existing
        );
        if (slots <= 0)
        {
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
