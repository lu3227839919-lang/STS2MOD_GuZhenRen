using System.Runtime.CompilerServices;

using GuZhenRen.Cards.XueDao;
using GuZhenRen.Powers.XueDao;

using HarmonyLib;

using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;

namespace GuZhenRen.Patches;

/// <summary>
/// 血道牌完成击杀后获得遗骸。
///
/// 原版 Hook.BeforeCardPlayed / Hook.AfterCardPlayed 是每张牌出牌时
/// 都会执行的全局钩子。本补丁为“血道效果牌”（血道蛊虫、血道杀招/
/// 衍生牌）记录出牌前存活敌人，出牌结算后对比死亡的主敌人，生成遗骸。
///
/// 被血道寄生的普通宿主牌由 XueJiPower → XueDaoParasiteSystem 的
/// 寄生路径生成遗骸，这里显式排除，避免同一击杀重复产生。
/// 流血致死（LiuXuePower）的遗骸来源保持不变。
/// </summary>
internal static class XueDaoRemainsKillPatch
{
    private const string HarmonyId =
        Entry.ModId + ".XueDaoRemainsKill";

    private static readonly ConditionalWeakTable<
        Player,
        uint[]
    > AliveBeforeByPlayer = new();

    private static bool _initialized;

    internal static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        Harmony harmony = new(HarmonyId);

        harmony.Patch(
            AccessTools.Method(
                typeof(Hook),
                nameof(Hook.BeforeCardPlayed)
            ) ?? throw new MissingMethodException(
                typeof(Hook).FullName,
                nameof(Hook.BeforeCardPlayed)
            ),
            prefix: new HarmonyMethod(
                typeof(XueDaoRemainsKillPatch),
                nameof(BeforeCardPlayedPrefix)
            )
        );

        harmony.Patch(
            AccessTools.Method(
                typeof(Hook),
                nameof(Hook.AfterCardPlayed)
            ) ?? throw new MissingMethodException(
                typeof(Hook).FullName,
                nameof(Hook.AfterCardPlayed)
            ),
            postfix: new HarmonyMethod(
                typeof(XueDaoRemainsKillPatch),
                nameof(AfterCardPlayedPostfix)
            )
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

    private static void BeforeCardPlayedPrefix(
        ICombatState combatState,
        CardPlay cardPlay
    )
    {
        if (!cardPlay.IsFirstInSeries ||
            !XueDaoPowerSystem.IsXueDaoEffectCard(cardPlay.Card))
        {
            return;
        }

        AliveBeforeByPlayer.Remove(cardPlay.Player);
        AliveBeforeByPlayer.Add(
            cardPlay.Player,
            combatState.HittableEnemies
                .Where(enemy => enemy.IsAlive)
                .Select(enemy => enemy.CombatId)
                .OfType<uint>()
                .ToArray()
        );
    }

    private static async Task AfterCardPlayedPostfix(
        ICombatState combatState,
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        if (!cardPlay.IsLastInSeries ||
            !XueDaoPowerSystem.IsXueDaoEffectCard(cardPlay.Card) ||
            // 寄生宿主由寄生路径（XueJiPower → TriggerFromCardPlayAsync）
            // 统一生成遗骸，此处跳过避免重复。
            XueDaoParasiteSystem.HasParasite(cardPlay.Card))
        {
            return;
        }

        if (!AliveBeforeByPlayer.TryGetValue(
                cardPlay.Player,
                out uint[]? aliveBefore) ||
            aliveBefore == null)
        {
            return;
        }

        AliveBeforeByPlayer.Remove(cardPlay.Player);

        await XueDaoParasiteSystem.CreateRemainsForNewDeaths(
            cardPlay.Player,
            aliveBefore
        );
    }
}
