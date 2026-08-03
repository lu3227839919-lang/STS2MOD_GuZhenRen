using System.Reflection;
using System.Threading;

using HarmonyLib;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;

namespace GuZhenRen.Cards;

internal static class CardUniquenessPatch
{
    private const string HarmonyId =
        Entry.ModId + ".CardUniqueness";

    private static readonly AsyncLocal<TransformationPlan?>
        ActiveTransformationPlan = new();

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
            AccessTools.Method(
                typeof(Hook),
                nameof(Hook.ShouldAddToDeck),
                [
                    typeof(IRunState),
                    typeof(CardModel),
                    typeof(AbstractModel).MakeByRefType(),
                ]
            ),
            postfix: nameof(ShouldAddToDeckPostfix)
        );

        Patch(
            harmony,
            AccessTools.Method(
                typeof(Hook),
                nameof(Hook.ModifyMerchantCardPool),
                [
                    typeof(IRunState),
                    typeof(Player),
                    typeof(IEnumerable<CardModel>),
                ]
            ),
            postfix: nameof(MerchantPoolPostfix)
        );

        Patch(
            harmony,
            AccessTools.Method(
                typeof(CardCreationOptions),
                nameof(CardCreationOptions.GetPossibleCards),
                [typeof(Player)]
            ),
            postfix: nameof(RewardPoolPostfix)
        );

        Patch(
            harmony,
            AccessTools.Method(
                typeof(Hook),
                nameof(Hook.TryModifyCardRewardOptions),
                [
                    typeof(IRunState),
                    typeof(Player),
                    typeof(List<CardCreationResult>),
                    typeof(CardCreationOptions),
                    typeof(List<AbstractModel>).MakeByRefType(),
                ]
            ),
            postfix: nameof(RewardResultsPostfix)
        );

        Patch(
            harmony,
            AccessTools.Method(
                typeof(CardFactory),
                nameof(CardFactory.CreateForReward),
                [
                    typeof(Player),
                    typeof(int),
                    typeof(CardCreationOptions),
                ]
            ),
            prefix: nameof(CreateForRewardPrefix)
        );

        Patch(
            harmony,
            AccessTools.Method(
                typeof(CardFactory),
                "GetFilteredTransformationOptions",
                [
                    typeof(CardModel),
                    typeof(IEnumerable<CardModel>),
                    typeof(bool),
                ]
            ),
            postfix: nameof(TransformationOptionsPostfix)
        );

        // 单张显式转换最终也会调用这个批量重载。
        PatchBatchTransform(
            harmony,
            AccessTools.Method(
                typeof(CardCmd),
                nameof(CardCmd.Transform),
                [
                    typeof(IEnumerable<CardTransformation>),
                    typeof(Rng),
                    typeof(CardPreviewStyle),
                ]
            )
        );

        Patch(
            harmony,
            AccessTools.PropertyGetter(
                typeof(CardModel),
                nameof(CardModel.Tags)
            ),
            postfix: nameof(TagsPostfix)
        );

        Patch(
            harmony,
            AccessTools.Method(
                typeof(CardModel),
                nameof(CardModel.GetKeywordsWithSources),
                [typeof(KeywordSources)]
            ),
            postfix: nameof(KeywordsPostfix)
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
            ActiveTransformationPlan.Value = null;
            _initialized = false;
        }
    }

    private static void PatchBatchTransform(
        Harmony harmony,
        MethodInfo? original
    )
    {
        if (original == null)
        {
            throw new MissingMethodException(
                "唯一规则所需的原游戏转换方法不存在。"
            );
        }

        harmony.Patch(
            original,
            prefix: new HarmonyMethod(
                typeof(CardUniquenessPatch),
                nameof(BatchTransformPrefix)
            ),
            postfix: new HarmonyMethod(
                typeof(CardUniquenessPatch),
                nameof(BatchTransformPostfix)
            ),
            finalizer: new HarmonyMethod(
                typeof(CardUniquenessPatch),
                nameof(BatchTransformFinalizer)
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
                "唯一规则所需的原游戏方法不存在。"
            );
        }

        harmony.Patch(
            original,
            prefix: prefix == null
                ? null
                : new HarmonyMethod(
                    typeof(CardUniquenessPatch),
                    prefix
                ),
            postfix: postfix == null
                ? null
                : new HarmonyMethod(
                    typeof(CardUniquenessPatch),
                    postfix
                )
        );
    }

    [HarmonyPriority(Priority.Last)]
    private static void ShouldAddToDeckPostfix(
        IRunState runState,
        CardModel card,
        ref bool __result,
        ref AbstractModel? preventer
    )
    {
        // 先尊重游戏本体和其他模型的拒绝结果；只有原流程允许时，
        // 才执行会写入仙蛊仲裁状态的最终检查。
        if (!__result)
        {
            return;
        }

        if (GuZhenRenCardRules
            .TryAuthorizePermanentDeckEntry(
                runState,
                card
            ))
        {
            return;
        }

        preventer = card;
        __result = false;
    }

    private static void MerchantPoolPostfix(
        IRunState runState,
        Player player,
        ref IEnumerable<CardModel> __result
    )
    {
        __result = __result.Where(card =>
            GuZhenRenCardRules.CanOfferToPlayer(
                runState,
                player,
                card
            )
        ).ToArray();
    }

    private static void RewardPoolPostfix(
        Player player,
        ref IEnumerable<CardModel> __result
    )
    {
        __result = __result.Where(card =>
            GuZhenRenCardRules.CanAppearInCardReward(
                player,
                card
            ) &&
            GuZhenRenCardRules.CanOfferToPlayer(
                player.RunState,
                player,
                card
            )
        ).ToArray();
    }

    private static void RewardResultsPostfix(
        IRunState runState,
        Player player,
        List<CardCreationResult> cardRewardOptions,
        ref bool __result
    )
    {
        int removed = cardRewardOptions.RemoveAll(result =>
            !GuZhenRenCardRules.CanAppearInCardReward(
                player,
                result.Card
            ) ||
            !GuZhenRenCardRules.CanOfferToPlayer(
                runState,
                player,
                result.Card
            )
        );

        if (removed > 0)
        {
            __result = true;
        }
    }

    private static bool CreateForRewardPrefix(
        Player player,
        ref int cardCount,
        CardCreationOptions options,
        ref IEnumerable<CardCreationResult> __result
    )
    {
        CardModel[] possibleCards = options
            .GetPossibleCards(player)
            .Where(card =>
                GuZhenRenCardRules.CanAppearInCardReward(
                    player,
                    card
                )
            )
            .Where(card =>
                options.RarityOdds != CardRarityOddsType.Uniform ||
                (
                    card.Rarity != CardRarity.Basic &&
                    card.Rarity != CardRarity.Ancient
                )
            )
            .GroupBy(card => card.Id)
            .Select(group => group.First())
            .ToArray();

        if (possibleCards.Length == 0)
        {
            Entry.Logger.Info(
                $"玩家 {player.NetId} 的卡牌奖励没有符合奖励规则的候选牌。"
            );
            __result = Array.Empty<CardCreationResult>();
            return false;
        }

        cardCount = Math.Min(cardCount, possibleCards.Length);
        return true;
    }

    private static void TransformationOptionsPostfix(
        CardModel original,
        ref CardModel[] __result
    )
    {
        TransformationPlan? plan =
            ActiveTransformationPlan.Value;

        __result = __result.Where(replacement =>
            GuZhenRenCardRules.CanUseAsTransformationResult(
                original,
                replacement,
                plan?.IgnoredOriginals,
                plan?.PlannedAdditions
            )
        ).ToArray();
    }

    /// <summary>
    /// 先解析整批替换结果，再把输入改写为直接 Replacement。
    /// 因此原方法不会二次消费 RNG。
    ///
    /// 随机生成的替换牌会记录在 __state 中；原转换命令真正完成后，
    /// 未被实际采用的模型会通过游戏自身的 RemoveFromState 生命周期清理。
    /// </summary>
    private static void BatchTransformPrefix(
        ref IEnumerable<CardTransformation> transformations,
        Rng? rng,
        out TransformationExecutionState? __state
    )
    {
        __state = null;

        CardTransformation[] source =
            transformations.ToArray();

        if (source.Length == 0)
        {
            transformations = source;
            return;
        }

        TransformationPlan plan = new(
            source
                .Where(item =>
                    item.Original.Pile?.Type == PileType.Deck
                )
                .Select(item => item.Original)
        );

        TransformationExecutionState state =
            new();

        __state = state;

        TransformationPlan? previous =
            ActiveTransformationPlan.Value;
        ActiveTransformationPlan.Value = plan;

        try
        {
            List<CardTransformation> resolved =
                new(source.Length);

            foreach (CardTransformation item in source)
            {
                (CardModel? replacement, bool generated) =
                    ResolveReplacement(item, rng, plan);

                if (replacement == null)
                {
                    Entry.Logger.Info(
                        $"跳过 {item.Original.Id} 的转换：没有符合唯一规则的候选。"
                    );
                    continue;
                }

                if (!GuZhenRenCardRules
                    .CanUseAsTransformationResult(
                        item.Original,
                        replacement,
                        plan.IgnoredOriginals,
                        plan.PlannedAdditions
                    ))
                {
                    if (generated)
                    {
                        RemoveGeneratedReplacement(
                            item.Original,
                            replacement
                        );
                    }

                    Entry.Logger.Info(
                        $"阻止将 {item.Original.Id} 转换为 {replacement.Id}：违反唯一规则。"
                    );
                    continue;
                }

                plan.PlannedAdditions.Add(
                    new PlannedCardAddition(
                        item.Original.Owner,
                        replacement
                    )
                );

                if (generated)
                {
                    state.GeneratedReplacements.Add(
                        new GeneratedReplacement(
                            item.Original,
                            replacement
                        )
                    );
                }

                resolved.Add(
                    new CardTransformation(
                        item.Original,
                        replacement
                    )
                );
            }

            transformations = resolved;
        }
        catch
        {
            CleanupGeneratedReplacements(state);
            __state = null;
            throw;
        }
        finally
        {
            ActiveTransformationPlan.Value = previous;
        }
    }

    private static void BatchTransformPostfix(
        ref Task<IEnumerable<CardPileAddResult>> __result,
        TransformationExecutionState? __state
    )
    {
        __result = AwaitTransformAndCleanup(
            __result,
            __state
        );
    }

    private static Exception? BatchTransformFinalizer(
        Exception? __exception,
        TransformationExecutionState? __state
    )
    {
        // 异步异常由包装后的 Task finally 清理。
        // 这里只处理原方法在返回 Task 之前同步抛出的情况。
        if (__exception != null)
        {
            CleanupGeneratedReplacements(__state);
        }

        return __exception;
    }

    private static async Task<IEnumerable<CardPileAddResult>>
        AwaitTransformAndCleanup(
            Task<IEnumerable<CardPileAddResult>> task,
            TransformationExecutionState? state
        )
    {
        try
        {
            return await task;
        }
        finally
        {
            CleanupGeneratedReplacements(state);
        }
    }

    private static void CleanupGeneratedReplacements(
        TransformationExecutionState? state
    )
    {
        if (state == null ||
            state.CleanupCompleted)
        {
            return;
        }

        state.CleanupCompleted = true;

        foreach (
            GeneratedReplacement generated
            in state.GeneratedReplacements
        )
        {
            try
            {
                RemoveGeneratedReplacement(
                    generated.Original,
                    generated.Replacement
                );
            }
            catch (Exception exception)
            {
                // 清理失败不应覆盖原转换命令的成功或异常结果。
                Entry.Logger.Info(
                    $"清理转换孤儿牌 {generated.Replacement.Id} 失败：{exception}"
                );
            }
        }

        state.GeneratedReplacements.Clear();
    }

    private static (CardModel? Card, bool Generated)
        ResolveReplacement(
            CardTransformation transformation,
            Rng? rng,
            TransformationPlan plan
        )
    {
        if (transformation.Replacement != null)
        {
            return (transformation.Replacement, false);
        }

        if (rng == null)
        {
            throw new ArgumentException(
                "随机转换必须提供 RNG。",
                nameof(rng)
            );
        }

        try
        {
            IEnumerable<CardModel> candidates =
                transformation.ReplacementOptions ??
                CardFactory.GetDefaultTransformationOptions(
                    transformation.Original,
                    transformation.IsInCombat
                );

            CardModel[] filtered = candidates.Where(candidate =>
                GuZhenRenCardRules.CanUseAsTransformationResult(
                    transformation.Original,
                    candidate,
                    plan.IgnoredOriginals,
                    plan.PlannedAdditions
                )
            ).ToArray();

            if (filtered.Length == 0)
            {
                return (null, false);
            }

            CardModel replacement =
                CardFactory.CreateRandomCardForTransform(
                    transformation.Original,
                    filtered,
                    transformation.IsInCombat,
                    rng
                );

            return (replacement, true);
        }
        catch (InvalidOperationException exception)
            when (
                exception.Message.StartsWith(
                    "All transformation options provided are invalid!",
                    StringComparison.Ordinal
                )
            )
        {
            return (null, false);
        }
    }

    private static void RemoveGeneratedReplacement(
        CardModel original,
        CardModel replacement
    )
    {
        if (!ReferenceEquals(
                replacement.Owner,
                original.Owner
            ) ||
            replacement.HasBeenRemovedFromState)
        {
            return;
        }

        /*
         * 成功转换时，替换牌已经进入牌堆，原牌也已由原游戏
         * 标记为 HasBeenRemovedFromState；此时必须保留替换牌。
         *
         * 若原方法在 AddInternal 之后、原牌 RemoveFromState 之前
         * 抛出异常，替换牌虽已入堆，但原牌仍然有效。这里调用
         * 游戏自己的 RemoveFromState，把半完成的替换牌回滚掉。
         *
         * 替换牌从未入堆（例如候选被钩子替换）时，同样通过该
         * 生命周期标记为已移除，避免留下仍被视作有效的模型。
         */
        if (replacement.Pile == null ||
            !original.HasBeenRemovedFromState)
        {
            replacement.RemoveFromState();
        }
    }

    private static void TagsPostfix(
        CardModel __instance,
        ref IEnumerable<CardTag> __result
    )
    {
        if (!GuZhenRenCardRules.IsXianGu(__instance) ||
            __result.Contains(GuZhenRenTags.XianGu))
        {
            return;
        }

        __result = __result.Append(GuZhenRenTags.XianGu);
    }

    private static void KeywordsPostfix(
        CardModel __instance,
        KeywordSources sources,
        ref IReadOnlySet<CardKeyword> __result
    )
    {
        if (!sources.HasFlag(KeywordSources.Local))
        {
            return;
        }

        HashSet<CardKeyword> keywords =
            __result.ToHashSet();

        /*
         * 动态关键词可能在 MutableClone/读档过程中被复制进卡牌本地集合。
         * 因此每次都先清除旧的转数和仙蛊展示，再由当前保存状态重建，
         * 保证升转、降转、转换和多人快照恢复后不会显示过期标签。
         */
        keywords.ExceptWith(
            GuZhenRenKeywords.RankKeywords
        );
        keywords.Remove(
            GuZhenRenKeywords.XianGu
        );

        bool isXianGu =
            GuZhenRenCardRules.IsXianGu(__instance);

        if (isXianGu)
        {
            /*
             * 兼容旧版本已经把“唯一”复制进实例本地关键词的存档。
             * 仙蛊改为显示独立的“仙蛊”关键词；跨玩家整局唯一性
             * 仍由 GuZhenRenCardRules.IsXianGu 独立执行。
             */
            keywords.Remove(
                GuZhenRenKeywords.Unique
            );
            keywords.Add(
                GuZhenRenKeywords.XianGu
            );
        }

        if (GuZhenRenCardRules.TryGetDisplayGuRank(
                __instance,
                out int rank
            ))
        {
            keywords.Add(
                GuZhenRenKeywords.GetRankKeyword(rank)
            );
        }

        __result = keywords;
    }

    private readonly record struct GeneratedReplacement(
        CardModel Original,
        CardModel Replacement
    );

    private sealed class TransformationExecutionState
    {
        internal List<GeneratedReplacement>
            GeneratedReplacements { get; } = [];

        internal bool CleanupCompleted { get; set; }
    }

    private sealed class TransformationPlan
    {
        internal HashSet<CardModel> IgnoredOriginals { get; }

        internal List<PlannedCardAddition>
            PlannedAdditions { get; } = [];

        internal TransformationPlan(
            IEnumerable<CardModel> ignoredOriginals
        )
        {
            IgnoredOriginals =
                ignoredOriginals.ToHashSet();
        }
    }
}
