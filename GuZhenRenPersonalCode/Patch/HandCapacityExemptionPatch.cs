using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

using GuZhenRen.Cards.LiDao;
using GuZhenRen.Cards.ShaZhao;

using HarmonyLib;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;

namespace GuZhenRen.Cards;

/// <summary>
/// 普通手牌容量豁免：力道虚影与“杀招推演”仍实际位于原版手牌，
/// 因而保留全部显示、选取、保留和触发行为；只有在计算原版手牌
/// 上限时不计入数量。
///
/// 尖塔2的手牌上限检查分散在 CardPileCmd 与少数“补到满手”的卡牌
/// 内部，因此这里统一把这些容量计算替换为“仅统计占容量的牌”。
/// </summary>
internal static class HandCapacityExemptionPatch
{
    private const string HarmonyId =
        Entry.ModId + ".HandCapacityExemption";

    private static readonly MethodInfo MaxCardsInHandGetter =
        AccessTools.PropertyGetter(
            typeof(CardPile),
            nameof(CardPile.MaxCardsInHand)
        ) ?? throw new MissingMethodException(
            "找不到 CardPile.MaxCardsInHand。"
        );

    private static readonly MethodInfo CapacityCountMethod =
        AccessTools.Method(
            typeof(HandCapacityExemptionPatch),
            nameof(CountTowardHandLimit),
            [typeof(IEnumerable<CardModel>)]
        ) ?? throw new MissingMethodException(
            "找不到手牌容量统计方法。"
        );

    private static bool _initialized;

    internal static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        Harmony harmony = new(HarmonyId);

        // 所有常规抽牌和“生成/移动到手牌”的最终容量判定。
        PatchAsync(
            harmony,
            AccessTools.Method(
                typeof(CardPileCmd),
                "DrawInternal",
                [
                    typeof(PlayerChoiceContext),
                    typeof(decimal),
                    typeof(Player),
                    typeof(bool),
                ]
            )
        );

        PatchAsync(
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
                    typeof(bool),
                ]
            )
        );

        PatchDirect(
            harmony,
            AccessTools.Method(
                typeof(CardPileCmd),
                "CheckIfDrawIsPossibleAndShowThoughtBubbleIfNot",
                [typeof(Player)]
            )
        );

        // 这些原版牌会自行先算“离 10 张还差多少”，不经过 DrawInternal
        // 的请求数量计算。同步改成容量统计，避免虚影/推演让它们少补牌。
        PatchOnPlay<Anointed>(harmony);
        PatchOnPlay<CrashLanding>(harmony);
        PatchOnPlay<Dredge>(harmony);
        PatchOnPlay<NeowsFury>(harmony);
        PatchOnPlay<Pillage>(harmony);
        PatchOnPlay<Scrawl>(harmony);

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

    /// <summary>
    /// 只有力道虚影和“杀招推演”不占普通手牌容量。
    /// 其他虚影流派与杀招本体仍按正常手牌计算。
    /// </summary>
    internal static bool IsCapacityExempt(CardModel card) =>
        card is AbstractLiDaoXuYing or ShaZhaoTuiYan;

    /// <summary>
    /// 生成一张容量豁免牌并强制放入普通手牌。
    /// 即使已有 10 张正常手牌也不会被原版逻辑改送弃牌堆。
    /// </summary>
    internal static async Task<bool> AddGeneratedExemptToHandAsync(
        CardModel card,
        Player owner
    )
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(owner);

        if (!IsCapacityExempt(card))
        {
            throw new ArgumentException(
                "只有容量豁免牌可以使用该入手牌入口。",
                nameof(card)
            );
        }

        CardPile hand = PileType.Hand.GetPile(owner);
        if (ReferenceEquals(card.Pile, hand))
        {
            return true;
        }

        CardPileAddResult result;
        if (CountTowardHandLimit(hand.Cards) >=
            CardPile.MaxCardsInHand)
        {
            // 原版 Add 无法知道“当前要加入的牌本身不占容量”。
            // 满 10 张正常牌时先把新牌登记进战斗弃牌堆，再做一次
            // 不经过容量检查的牌堆迁移；这样不会弹出“手牌已满”。
            result = await CardPileCmd.AddGeneratedCardToCombat(
                card,
                PileType.Discard,
                owner
            );

            if (result.success)
            {
                await ForceMoveToHandAsync(card, hand);
            }
        }
        else
        {
            result = await CardPileCmd.AddGeneratedCardToCombat(
                card,
                PileType.Hand,
                owner
            );

            // 兼容游戏版本变化或其他模组的手牌补丁：如果原版仍把
            // 豁免牌送去了弃牌堆，再校正回手牌。
            if (result.success && !ReferenceEquals(card.Pile, hand))
            {
                await ForceMoveToHandAsync(card, hand);
            }
        }

        return result.success && ReferenceEquals(card.Pile, hand);
    }

    /// <summary>
    /// 推演成功生成的杀招允许在生成瞬间临时突破普通手牌容量。
    /// 杀招本身并非容量豁免牌，之后仍会正常计入手牌上限。
    /// </summary>
    internal static async Task<bool> AddGeneratedShaZhaoToHandAsync(
        AbstractShaZhaoCard card,
        Player owner
    )
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(owner);

        CardPile hand = PileType.Hand.GetPile(owner);
        if (ReferenceEquals(card.Pile, hand))
        {
            return true;
        }

        CardPileAddResult result =
            await CardPileCmd.AddGeneratedCardToCombat(
                card,
                CountTowardHandLimit(hand.Cards) < CardPile.MaxCardsInHand
                    ? PileType.Hand
                    : PileType.Discard,
                owner
            );

        if (result.success && !ReferenceEquals(card.Pile, hand))
        {
            await ForceMoveToHandAsync(card, hand);
        }

        return result.success && ReferenceEquals(card.Pile, hand);
    }

    /// <summary>
    /// 已在战斗中的容量豁免牌返回普通手牌时使用。
    /// 主要覆盖虚影显化后的回手与推演取消/失败后的回手。
    /// </summary>
    internal static async Task MoveExemptToHandAsync(
        CardModel card,
        bool skipVisuals
    )
    {
        ArgumentNullException.ThrowIfNull(card);

        if (!IsCapacityExempt(card))
        {
            throw new ArgumentException(
                "只有容量豁免牌可以使用该回手入口。",
                nameof(card)
            );
        }

        Player owner = card.Owner;
        CardPile hand = PileType.Hand.GetPile(owner);
        if (ReferenceEquals(card.Pile, hand))
        {
            return;
        }

        if (CountTowardHandLimit(hand.Cards) >=
            CardPile.MaxCardsInHand)
        {
            await ForceMoveToHandAsync(card, hand);
            return;
        }

        await CardPileCmd.Add(
            card,
            hand,
            CardPilePosition.Bottom,
            clonedBy: null,
            skipVisuals: skipVisuals
        );

        if (!ReferenceEquals(card.Pile, hand))
        {
            await ForceMoveToHandAsync(card, hand);
        }
    }

    /// <summary>
    /// 仅用于容量豁免牌的最终校正。保持卡牌仍属于原版 Hand 牌堆，
    /// 同时补发 AfterCardChangedPiles，避免直接内部移牌漏掉模型监听。
    /// </summary>
    private static async Task ForceMoveToHandAsync(
        CardModel card,
        CardPile hand
    )
    {
        CardPile? source = card.Pile;
        if (ReferenceEquals(source, hand))
        {
            return;
        }

        PileType oldPile = source?.Type ?? PileType.None;
        source?.RemoveInternal(card, silent: true);
        hand.AddInternal(card, silent: true);

        source?.InvokeContentsChanged();
        hand.InvokeContentsChanged();

        await Hook.AfterCardChangedPiles(
            card.Owner.RunState,
            card.CombatState,
            card,
            oldPile,
            clonedBy: null
        );
    }

    private static int CountTowardHandLimit(
        IEnumerable<CardModel> cards
    ) => cards.Count(static card => !IsCapacityExempt(card));

    private static void PatchOnPlay<TCard>(Harmony harmony)
        where TCard : CardModel
    {
        MethodInfo? onPlay = AccessTools.DeclaredMethod(
            typeof(TCard),
            "OnPlay",
            [typeof(PlayerChoiceContext), typeof(CardPlay)]
        );
        PatchAsync(harmony, onPlay);
    }

    private static void PatchAsync(
        Harmony harmony,
        MethodInfo? asyncMethod
    )
    {
        if (asyncMethod == null)
        {
            throw new MissingMethodException(
                "手牌容量豁免所需的异步方法不存在。"
            );
        }

        AsyncStateMachineAttribute? attribute =
            asyncMethod.GetCustomAttribute<AsyncStateMachineAttribute>();
        if (attribute == null)
        {
            throw new MissingMethodException(
                $"{asyncMethod.DeclaringType?.FullName}.{asyncMethod.Name} " +
                "不是可识别的 async 状态机。"
            );
        }

        MethodInfo? moveNext = AccessTools.Method(
            attribute.StateMachineType,
            nameof(IAsyncStateMachine.MoveNext)
        );
        PatchDirect(harmony, moveNext);
    }

    private static void PatchDirect(
        Harmony harmony,
        MethodInfo? method
    )
    {
        if (method == null)
        {
            throw new MissingMethodException(
                "手牌容量豁免所需的方法不存在。"
            );
        }

        harmony.Patch(
            method,
            transpiler: new HarmonyMethod(
                typeof(HandCapacityExemptionPatch),
                nameof(CapacityCountTranspiler)
            )
        );
    }

    /// <summary>
    /// 只替换与 MaxCardsInHand 同一小段表达式中的 CardModel 集合 Count，
    /// 不碰普通的牌堆排序、随机插入位置、循环计数等 Count 调用。
    /// </summary>
    private static IEnumerable<CodeInstruction> CapacityCountTranspiler(
        IEnumerable<CodeInstruction> instructions
    )
    {
        List<CodeInstruction> list = instructions.ToList();
        int replacements = 0;

        for (int index = 0; index < list.Count; index++)
        {
            if (!IsCardCollectionCount(list[index]) ||
                !HasMaxCardsInHandNearby(list, index))
            {
                continue;
            }

            list[index].opcode = OpCodes.Call;
            list[index].operand = CapacityCountMethod;
            replacements++;
        }

        if (replacements == 0)
        {
            Entry.Logger.Warn(
                "[手牌容量豁免] 未在目标方法中找到可替换的手牌 Count；" +
                "当前游戏版本的手牌上限实现可能已变化。"
            );
        }

        return list;
    }

    private static bool HasMaxCardsInHandNearby(
        IReadOnlyList<CodeInstruction> instructions,
        int index
    )
    {
        const int radius = 16;
        int start = Math.Max(0, index - radius);
        int end = Math.Min(instructions.Count - 1, index + radius);

        for (int cursor = start; cursor <= end; cursor++)
        {
            if (instructions[cursor].Calls(MaxCardsInHandGetter))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsCardCollectionCount(CodeInstruction instruction)
    {
        if (instruction.operand is not MethodInfo method ||
            method.ReturnType != typeof(int))
        {
            return false;
        }

        if (method.DeclaringType == typeof(Enumerable) &&
            method.Name == nameof(Enumerable.Count) &&
            method.IsGenericMethod &&
            method.GetGenericArguments() is [Type elementType] &&
            elementType == typeof(CardModel))
        {
            return true;
        }

        if (method.Name != "get_Count" || method.DeclaringType == null)
        {
            return false;
        }

        Type declaringType = method.DeclaringType;
        if (!declaringType.IsGenericType)
        {
            return false;
        }

        Type[] genericArguments = declaringType.GetGenericArguments();
        return genericArguments.Length == 1 &&
            genericArguments[0] == typeof(CardModel);
    }
}
