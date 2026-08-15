using System.Reflection;
using System.Threading;

using Godot;
using HarmonyLib;

using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;

namespace GuZhenRen.Patches;

/// <summary>
/// 防止卡牌奖励在生成后被遗物等后续修饰清成空列表。
///
/// 初次生成确实没有候选时，仍由现有奖励清理逻辑移除；只有已经
/// 存在候选的奖励在后续 Hook 中被全部删除时，才恢复调用前的列表。
/// 同时兼容只有“跳过/重掷”等替代选项的空卡牌界面。
/// </summary>
internal static class EmptyCardRewardCompatibilityPatch
{
    private const string HarmonyId =
        Entry.ModId + ".EmptyCardRewardCompatibility";

    private static readonly AsyncLocal<int>
        RewardCreationDepth = new();

    private static bool _initialized;

    internal static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        MethodInfo? createForReward = AccessTools.Method(
            typeof(CardFactory),
            nameof(CardFactory.CreateForReward),
            [
                typeof(Player),
                typeof(int),
                typeof(CardCreationOptions),
            ]
        );
        MethodInfo? modifyRewardOptions = AccessTools.Method(
            typeof(Hook),
            nameof(Hook.TryModifyCardRewardOptions),
            [
                typeof(IRunState),
                typeof(Player),
                typeof(List<CardCreationResult>),
                typeof(CardCreationOptions),
                typeof(List<AbstractModel>).MakeByRefType(),
            ]
        );
        MethodInfo? selectCardReward =
            AccessTools.DeclaredMethod(
                typeof(CardReward),
                "OnSelect"
            );
        MethodInfo? defaultFocusedControl =
            AccessTools.PropertyGetter(
                typeof(NCardRewardSelectionScreen),
                nameof(
                    NCardRewardSelectionScreen
                        .DefaultFocusedControl
                )
            );

        if (createForReward == null ||
            modifyRewardOptions == null ||
            selectCardReward == null ||
            defaultFocusedControl == null)
        {
            throw new MissingMethodException(
                "空卡牌奖励兼容补丁所需的游戏方法不存在。"
            );
        }

        Harmony harmony = new(HarmonyId);
        harmony.Patch(
            createForReward,
            prefix: new HarmonyMethod(
                typeof(EmptyCardRewardCompatibilityPatch),
                nameof(CreateForRewardPrefix)
            ),
            postfix: new HarmonyMethod(
                typeof(EmptyCardRewardCompatibilityPatch),
                nameof(CreateForRewardPostfix)
            ),
            finalizer: new HarmonyMethod(
                typeof(EmptyCardRewardCompatibilityPatch),
                nameof(CreateForRewardFinalizer)
            )
        );
        harmony.Patch(
            modifyRewardOptions,
            prefix: new HarmonyMethod(
                typeof(EmptyCardRewardCompatibilityPatch),
                nameof(ModifyRewardOptionsPrefix)
            ),
            postfix: new HarmonyMethod(
                typeof(EmptyCardRewardCompatibilityPatch),
                nameof(ModifyRewardOptionsPostfix)
            )
        );
        harmony.Patch(
            selectCardReward,
            prefix: new HarmonyMethod(
                typeof(EmptyCardRewardCompatibilityPatch),
                nameof(CardRewardOnSelectPrefix)
            )
        );
        harmony.Patch(
            defaultFocusedControl,
            prefix: new HarmonyMethod(
                typeof(EmptyCardRewardCompatibilityPatch),
                nameof(DefaultFocusedControlPrefix)
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
            RewardCreationDepth.Value = 0;
            _initialized = false;
        }
    }

    private static void CreateForRewardPrefix(
        out int __state
    )
    {
        __state = RewardCreationDepth.Value;
        RewardCreationDepth.Value++;
    }

    private static void CreateForRewardPostfix(int __state)
    {
        RewardCreationDepth.Value = __state;
    }

    private static Exception? CreateForRewardFinalizer(
        Exception? __exception,
        int __state
    )
    {
        RewardCreationDepth.Value = __state;
        return __exception;
    }

    private static void ModifyRewardOptionsPrefix(
        List<CardCreationResult> cardRewardOptions,
        out CardCreationResult[] __state
    )
    {
        __state = cardRewardOptions.ToArray();
    }

    [HarmonyPriority(Priority.Last)]
    private static void ModifyRewardOptionsPostfix(
        Player player,
        List<CardCreationResult> cardRewardOptions,
        CardCreationResult[] __state,
        ref bool __result
    )
    {
        if (RewardCreationDepth.Value > 0 ||
            __state.Length == 0 ||
            cardRewardOptions.Count > 0)
        {
            return;
        }

        cardRewardOptions.AddRange(__state);
        __result = true;

        Entry.Logger.Warn(
            $"玩家 {player.NetId} 的现有卡牌奖励在后续修饰后变为空；" +
            "已恢复修饰前的候选，避免生成空奖励界面。"
        );
    }

    private static bool CardRewardOnSelectPrefix(
        CardReward __instance,
        ref Task<bool> __result
    )
    {
        if (__instance.IsPopulated ||
            CardRewardAlternative.Generate(__instance).Count > 0)
        {
            return true;
        }

        Entry.Logger.Warn(
            $"玩家 {__instance.Player.NetId} 选择了没有候选牌或" +
            "替代选项的卡牌奖励；已安全结算该空奖励。"
        );
        __result = Task.FromResult(true);
        return false;
    }

    private static bool DefaultFocusedControlPrefix(
        NCardRewardSelectionScreen __instance,
        ref Control __result
    )
    {
        Control cardRow = __instance.GetNode<Control>(
            "UI/CardRow"
        );

        if (cardRow.GetChildren()
            .OfType<NGridCardHolder>()
            .Any())
        {
            return true;
        }

        Control alternatives = __instance.GetNode<Control>(
            "UI/RewardAlternatives"
        );
        __result = alternatives.GetChildren()
            .OfType<Control>()
            .FirstOrDefault() ?? __instance;
        return false;
    }
}
