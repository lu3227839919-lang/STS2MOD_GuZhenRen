using System.Reflection;
using System.Threading;

using GuZhenRen.Cards.Basic;
using HarmonyLib;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRen.Patches;

internal static class ShaZhaoTuiYanPatch
{
    private const string HarmonyId =
        Entry.ModId + ".ShaZhaoTuiYan";

    // CardPile.MaxCardsInHand 是静态属性；
    // 用异步上下文标记当前正在处理的玩家，避免多人串号。
    private static readonly AsyncLocal<Player?>
        HandLimitPlayer = new();

    private static bool _initialized;

    internal static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        Harmony harmony = new(HarmonyId);

        Patch(
            harmony,
            AccessTools.PropertyGetter(
                typeof(CardPile),
                nameof(CardPile.MaxCardsInHand)
            ),
            postfix: nameof(HandLimitPostfix)
        );

        PatchContextMethod(
            harmony,
            AccessTools.Method(
                typeof(CardPileCmd),
                nameof(CardPileCmd.Draw),
                [
                    typeof(PlayerChoiceContext),
                    typeof(decimal),
                    typeof(Player),
                    typeof(bool),
                ]
            ),
            nameof(DrawContextPrefix),
            nameof(DrawContextPostfix)
        );

        PatchContextMethod(
            harmony,
            AccessTools.Method(
                typeof(CardPileCmd),
                nameof(CardPileCmd.Add),
                [
                    typeof(IEnumerable<CardModel>),
                    typeof(CardPile),
                    typeof(CardPilePosition),
                    typeof(AbstractModel),
                    typeof(bool),
                ]
            ),
            nameof(AddContextPrefix),
            nameof(AddContextPostfix)
        );

        Patch(
            harmony,
            AccessTools.Method(
                typeof(CardCmd),
                nameof(CardCmd.Discard),
                [
                    typeof(PlayerChoiceContext),
                    typeof(CardModel),
                ]
            ),
            prefix: nameof(DiscardSinglePrefix)
        );

        Patch(
            harmony,
            AccessTools.Method(
                typeof(CardCmd),
                nameof(CardCmd.Discard),
                [
                    typeof(PlayerChoiceContext),
                    typeof(IEnumerable<CardModel>),
                ]
            ),
            prefix: nameof(DiscardManyPrefix)
        );

        // 部分卡牌会直接调用 DiscardAndDraw，绕过 Discard 重载。
        Patch(
            harmony,
            AccessTools.Method(
                typeof(CardCmd),
                nameof(CardCmd.DiscardAndDraw),
                [
                    typeof(PlayerChoiceContext),
                    typeof(IEnumerable<CardModel>),
                    typeof(int),
                ]
            ),
            prefix: nameof(DiscardAndDrawPrefix)
        );

        Patch(
            harmony,
            AccessTools.Method(
                typeof(CardCmd),
                nameof(CardCmd.Exhaust),
                [
                    typeof(PlayerChoiceContext),
                    typeof(CardModel),
                    typeof(bool),
                    typeof(bool),
                ]
            ),
            prefix: nameof(ExhaustPrefix)
        );

        // 单张重载最终调用该批量重载。
        Patch(
            harmony,
            AccessTools.Method(
                typeof(CardPileCmd),
                nameof(CardPileCmd.RemoveFromDeck),
                [
                    typeof(IReadOnlyList<CardModel>),
                    typeof(bool),
                ]
            ),
            prefix: nameof(RemoveFromDeckPrefix)
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
            HandLimitPlayer.Value = null;
            _initialized = false;
        }
    }

    private static void PatchContextMethod(
        Harmony harmony,
        MethodInfo? original,
        string prefix,
        string postfix
    )
    {
        if (original == null)
        {
            throw new MissingMethodException(
                "杀招推演所需的原游戏方法不存在。"
            );
        }

        harmony.Patch(
            original,
            prefix: new HarmonyMethod(
                typeof(ShaZhaoTuiYanPatch),
                prefix
            ),
            postfix: new HarmonyMethod(
                typeof(ShaZhaoTuiYanPatch),
                postfix
            ),
            finalizer: new HarmonyMethod(
                typeof(ShaZhaoTuiYanPatch),
                nameof(RestoreContextFinalizer)
            )
        );
    }

    private static void Patch(
        Harmony harmony,
        MethodInfo? original,
        string? prefix = null,
        string? postfix = null
    )
    {
        if (original == null)
        {
            throw new MissingMethodException(
                "杀招推演所需的原游戏方法不存在。"
            );
        }

        harmony.Patch(
            original,
            prefix: prefix == null
                ? null
                : new HarmonyMethod(
                    typeof(ShaZhaoTuiYanPatch),
                    prefix
                ),
            postfix: postfix == null
                ? null
                : new HarmonyMethod(
                    typeof(ShaZhaoTuiYanPatch),
                    postfix
                )
        );
    }

    private static void HandLimitPostfix(
        ref int __result
    )
    {
        Player? player = HandLimitPlayer.Value;

        if (player != null)
        {
            __result +=
                ShaZhaoTuiYan.CountCombatCopies(
                    player
                );
        }
    }

    private static void DrawContextPrefix(
        Player player,
        out Player? __state
    )
    {
        __state = HandLimitPlayer.Value;
        HandLimitPlayer.Value = player;
    }

    private static void AddContextPrefix(
        ref IEnumerable<CardModel> cards,
        out Player? __state
    )
    {
        __state = HandLimitPlayer.Value;

        /*
         * cards 可能是只能枚举一次的延迟序列。
         * 若这里只调用 FirstOrDefault，原方法再次枚举时可能丢失首张牌。
         * 先物化并把同一数组传回原方法，可避免消费输入序列。
         */
        CardModel[] materializedCards =
            cards as CardModel[] ??
            cards.ToArray();

        cards = materializedCards;
        HandLimitPlayer.Value =
            materializedCards.FirstOrDefault()?.Owner;
    }

    private static void DrawContextPostfix(
        ref Task<IEnumerable<CardModel>> __result,
        Player? __state
    )
    {
        Player? activePlayer = HandLimitPlayer.Value;

        try
        {
            __result = AwaitTaskWithContext(
                __result,
                activePlayer,
                __state
            );
        }
        finally
        {
            // 原调用者不应在等待命令完成期间继承当前玩家上下文。
            HandLimitPlayer.Value = __state;
        }
    }

    private static void AddContextPostfix(
        ref Task<IReadOnlyList<CardPileAddResult>> __result,
        Player? __state
    )
    {
        Player? activePlayer = HandLimitPlayer.Value;

        try
        {
            __result = AwaitTaskWithContext(
                __result,
                activePlayer,
                __state
            );
        }
        finally
        {
            HandLimitPlayer.Value = __state;
        }
    }

    private static Exception? RestoreContextFinalizer(
        Exception? __exception,
        Player? __state
    )
    {
        // 原方法同步抛出时 Postfix 不会执行，Finalizer 负责兜底恢复。
        HandLimitPlayer.Value = __state;
        return __exception;
    }

    private static async Task<T> AwaitTaskWithContext<T>(
        Task<T> task,
        Player? activePlayer,
        Player? previousPlayer
    )
    {
        HandLimitPlayer.Value = activePlayer;

        try
        {
            return await task;
        }
        finally
        {
            HandLimitPlayer.Value = previousPlayer;
        }
    }

    private static bool DiscardSinglePrefix(
        CardModel card,
        ref Task __result
    )
    {
        if (card is not ShaZhaoTuiYan)
        {
            return true;
        }

        __result = Task.CompletedTask;
        return false;
    }

    private static void DiscardManyPrefix(
        ref IEnumerable<CardModel> cards
    )
    {
        cards = cards.Where(
            card => card is not ShaZhaoTuiYan
        );
    }

    private static void DiscardAndDrawPrefix(
        ref IEnumerable<CardModel>
            cardsToDiscard
    )
    {
        cardsToDiscard =
            cardsToDiscard.Where(
                card =>
                    card is not ShaZhaoTuiYan
            );
    }

    private static bool ExhaustPrefix(
        CardModel card,
        ref Task __result
    )
    {
        if (card is not ShaZhaoTuiYan)
        {
            return true;
        }

        __result = Task.CompletedTask;
        return false;
    }

    private static void RemoveFromDeckPrefix(
        ref IReadOnlyList<CardModel> cards
    )
    {
        cards = cards
            .Where(card => card is not ShaZhaoTuiYan)
            .ToArray();
    }

}
