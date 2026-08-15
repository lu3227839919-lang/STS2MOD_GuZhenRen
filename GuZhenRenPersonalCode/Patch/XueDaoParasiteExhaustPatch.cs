using System.Reflection;

using HarmonyLib;

using GuZhenRen.Cards.XueDao;

using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRen.Cards;

/// <summary>
/// 捕获非出牌途径的消耗（虚无、其他卡牌效果等），使未成熟血寄执行破胎。
///
/// 使用 CardExhaustCompat 动态发现当前游戏的 CardCmd.Exhaust 重载，
/// 同时兼容 Task 与 Task&lt;T&gt; 返回值。找不到兼容方法时只记录警告，
/// 不再让整个模组 DLL 初始化失败。
/// </summary>
internal static class XueDaoParasiteExhaustPatch
{
    private const string HarmonyId =
        Entry.ModId + ".XueDaoParasiteExhaust";

    private static bool _initialized;

    internal static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        IReadOnlyList<MethodInfo> methods =
            CardExhaustCompat.FindPatchableMethods();

        if (methods.Count == 0)
        {
            Entry.Logger.Warn(
                "[血寄-破胎] 当前游戏未找到返回 Task/Task<T> 且包含 CardModel " +
                "参数的 CardCmd.Exhaust 重载；跳过直接消耗监听，" +
                "但不阻止模组初始化。"
            );

            _initialized = true;
            return;
        }

        Harmony harmony = new(HarmonyId);
        HarmonyMethod postfix = new(
            typeof(XueDaoParasiteExhaustPatch),
            nameof(ExhaustPostfix)
        );

        foreach (MethodInfo method in methods)
        {
            harmony.Patch(method, postfix: CreatePostfix(method, postfix));

            Entry.Logger.Info(
                "[血寄-破胎] 已挂载消耗监听：" +
                CardExhaustCompat.FormatSignature(method)
            );
        }

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

    private static void ExhaustPostfix(
        object[] __args,
        ref Task __result
    )
    {
        if (!CardExhaustCompat.TryReadArguments(
                __args,
                out PlayerChoiceContext choiceContext,
                out CardModel? card
            ) ||
            card == null ||
            !XueDaoParasiteSystem.HasParasite(card))
        {
            return;
        }

        __result = AwaitExhaustAndBreakAsync(
            __result,
            choiceContext,
            card
        );
    }

    private static HarmonyMethod CreatePostfix(
        MethodInfo target,
        HarmonyMethod nonGenericPostfix
    )
    {
        Type returnType = target.ReturnType;
        if (!returnType.IsGenericType ||
            returnType.GetGenericTypeDefinition() != typeof(Task<>))
        {
            return nonGenericPostfix;
        }

        MethodInfo genericPostfix = typeof(XueDaoParasiteExhaustPatch)
            .GetMethod(
                nameof(ExhaustPostfixGeneric),
                BindingFlags.NonPublic | BindingFlags.Static
            )
            ?.MakeGenericMethod(returnType.GetGenericArguments()[0])
            ?? throw new MissingMethodException(
                typeof(XueDaoParasiteExhaustPatch).FullName,
                nameof(ExhaustPostfixGeneric)
            );

        return new HarmonyMethod(genericPostfix);
    }

    private static void ExhaustPostfixGeneric<TResult>(
        object[] __args,
        ref Task<TResult> __result
    )
    {
        if (!CardExhaustCompat.TryReadArguments(
                __args,
                out PlayerChoiceContext choiceContext,
                out CardModel? card
            ) ||
            card == null ||
            !XueDaoParasiteSystem.HasParasite(card))
        {
            return;
        }

        __result = AwaitExhaustAndBreakAsync(
            __result,
            choiceContext,
            card
        );
    }

    private static async Task AwaitExhaustAndBreakAsync(
        Task exhaustTask,
        PlayerChoiceContext choiceContext,
        CardModel card
    )
    {
        await exhaustTask;

        await XueDaoParasiteSystem.BreakIfExhaustedAsync(
            choiceContext,
            card
        );
    }

    private static async Task<TResult> AwaitExhaustAndBreakAsync<TResult>(
        Task<TResult> exhaustTask,
        PlayerChoiceContext choiceContext,
        CardModel card
    )
    {
        TResult result = await exhaustTask;

        await XueDaoParasiteSystem.BreakIfExhaustedAsync(
            choiceContext,
            card
        );

        return result;
    }
}
