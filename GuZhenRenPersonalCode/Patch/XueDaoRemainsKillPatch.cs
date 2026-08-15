using GuZhenRen.Cards.XueDao;
using GuZhenRen.Powers.XueDao;

using HarmonyLib;

using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRen.Patches;

/// <summary>
/// 血道牌完成击杀后获得遗骸。
///
/// 原版击杀流程在敌人死亡后立即把尸体移出 CombatState
/// （CreatureCmd.Kill → combatState.RemoveCreature），因此“出牌前
/// 快照 + AfterCardPlayed 事后对比”无法检测死亡。本补丁改为在
/// Hook.AfterDeath（死亡瞬间，尸体仍有效）挂载：血道效果牌
/// （血道蛊虫、血道杀招/衍生、被寄生的宿主牌）开始结算时记录当前
/// 活动血道牌，敌人死亡时若存在活动血道牌即生成遗骸，每次出牌系列
/// 最多 2 张，另有永久牌堆 4 张上限约束总量。
///
/// 流血致死（LiuXuePower，不在出牌结算内）由原路径独立产生遗骸，
/// 与本补丁互不重复。
/// </summary>
internal static class XueDaoRemainsKillPatch
{
    private const string HarmonyId =
        Entry.ModId + ".XueDaoRemainsKill";

    private const int MaxRemainsPerPlay = 2;

    private static Player? _activePlayer;
    private static CardModel? _activeCard;
    private static int _remainsGrantedThisPlay;

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
                nameof(Hook.AfterDeath)
            ) ?? throw new MissingMethodException(
                typeof(Hook).FullName,
                nameof(Hook.AfterDeath)
            ),
            postfix: new HarmonyMethod(
                typeof(XueDaoRemainsKillPatch),
                nameof(AfterDeathPostfix)
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
            _activePlayer = null;
            _activeCard = null;
            _remainsGrantedThisPlay = 0;
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

        _activePlayer = cardPlay.Player;
        _activeCard = cardPlay.Card;
        _remainsGrantedThisPlay = 0;
    }

    private static void AfterDeathPostfix(
        ref Task __result,
        ICombatState? combatState,
        Creature creature
    )
    {
        if (_activePlayer == null ||
            _activeCard == null ||
            creature.Side != CombatSide.Enemy)
        {
            return;
        }

        __result = AwaitGrantRemainsOnDeathAsync(
            __result,
            creature
        );
    }

    private static async Task AwaitGrantRemainsOnDeathAsync(
        Task original,
        Creature creature
    )
    {
        await original;

        if (_activePlayer is not { } owner ||
            _activeCard == null ||
            _remainsGrantedThisPlay >= MaxRemainsPerPlay ||
            creature.Side != CombatSide.Enemy)
        {
            return;
        }

        _remainsGrantedThisPlay++;

        Entry.Logger.Info(
            $"[遗骸获取] {owner.NetId} 血道牌击杀 {creature.CombatId}，" +
            $"本系列已生成 {_remainsGrantedThisPlay}/{MaxRemainsPerPlay} 张遗骸。"
        );

        await XueDaoCardSystem.AddRemains(owner, 1);
    }

    private static void AfterCardPlayedPostfix(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        if (!cardPlay.IsLastInSeries ||
            !ReferenceEquals(_activeCard, cardPlay.Card))
        {
            return;
        }

        _activePlayer = null;
        _activeCard = null;
        _remainsGrantedThisPlay = 0;
    }
}
