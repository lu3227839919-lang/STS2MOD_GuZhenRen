using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

using HarmonyLib;

using GuZhenRen.Cards.Basic;
using GuZhenRen.Characters;
using GuZhenRen.Cards.XueDao;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace GuZhenRen.Cards;

internal static class CardUniquenessPatch
{
    private const string HarmonyId =
        Entry.ModId + ".CardUniqueness";

    private static readonly AsyncLocal<TransformationPlan?>
        ActiveTransformationPlan = new();

    // 卡面描述渲染：登记 LocString → 宿主牌，供 GetFormattedText 追加
    // 动态寄生文本。弱引用表，宿主牌与 LocString 被回收后自动清理。
    private static readonly ConditionalWeakTable<
        LocString,
        CardModel
    > DescriptionOwners = new();

    private static readonly AsyncLocal<int>
        RewardCandidateQueryDepth = new();

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

        MethodInfo? createForReward =
            AccessTools.Method(
                typeof(CardFactory),
                nameof(CardFactory.CreateForReward),
                [
                    typeof(Player),
                    typeof(int),
                    typeof(CardCreationOptions),
                ]
            );
        if (createForReward == null)
        {
            throw new MissingMethodException(
                "唯一规则所需的卡牌奖励生成方法不存在。"
            );
        }

        harmony.Patch(
            createForReward,
            prefix: new HarmonyMethod(
                typeof(CardUniquenessPatch),
                nameof(CreateForRewardPrefix)
            ),
            postfix: new HarmonyMethod(
                typeof(CardUniquenessPatch),
                nameof(CreateForRewardPostfix)
            ),
            finalizer: new HarmonyMethod(
                typeof(CardUniquenessPatch),
                nameof(CreateForRewardFinalizer)
            )
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
            prefix: nameof(TransformationOptionsPrefix)
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
            AccessTools.Method(
                typeof(CardCmd),
                nameof(CardCmd.TransformToRandom),
                [
                    typeof(CardModel),
                    typeof(Rng),
                    typeof(CardPreviewStyle),
                ]
            ),
            prefix: nameof(TransformToRandomPrefix)
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

        // 卡面/牌堆查看/升级预览最终都渲染 CardModel.Description
        // （LocString）。Description getter 返回 LocString（不能用
        // ref string），这里登记 LocString→卡牌 映射，再统一在
        // LocString.GetFormattedText 追加动态寄生文本，覆盖全部路径。
        Patch(
            harmony,
            AccessTools.PropertyGetter(
                typeof(CardModel),
                "Description"
            ),
            postfix: nameof(DescriptionRegisterPostfix)
        );

        Patch(
            harmony,
            AccessTools.DeclaredMethod(
                typeof(LocString),
                "GetFormattedText",
                Type.EmptyTypes
            ),
            postfix: nameof(GetFormattedTextPostfix)
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
            RewardCandidateQueryDepth.Value = 0;
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
                card,
                GuDeckCapacityReplacementPatch.CardBeingReplaced
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
        CardModel[] originalCandidates = __result.ToArray();

        // Hook.ModifyMerchantCardPool 同时处理角色卡槽和无色卡槽。
        // 无色槽的候选必须保持为原版无色池，不能套用“普通奖励只出
        // 蛊虫”的限制。空候选也按无色查询处理，避免错误注入蛊牌。
        bool isColorlessMerchantPool =
            originalCandidates.Length == 0 ||
            originalCandidates.All(static card =>
                card.Pool.IsColorless
            );

        IEnumerable<CardModel> candidates = originalCandidates;

        // 角色主池已经是蛊虫池，正常商店路径不再重复拼接。
        // 只有其他效果把有色候选完全替换掉时，才补回已解锁蛊牌，
        // 避免唯一性过滤后角色卡槽变成空槽。
        if (player.Character is GuZhenRenCharacter &&
            !isColorlessMerchantPool &&
            originalCandidates.All(static card =>
                card.Pool is not GuZhenRenGuCardPool
            ))
        {
            candidates = candidates.Concat(
                GetUnlockedGuCards(player)
            );
        }

        __result = candidates
            .GroupBy(card => card.Id)
            .Select(group => group.First())
            .Where(card =>
                isColorlessMerchantPool ||
                player.Character is not GuZhenRenCharacter ||
                GuZhenRenCardRules.CanAppearInCardReward(
                    player,
                    card
                )
            )
            .Where(card =>
                GuZhenRenCardRules.CanOfferWithGuReplacement(
                    runState,
                    player,
                    card
                )
            )
            .ToArray();
    }

    private static void RewardPoolPostfix(
        CardCreationOptions __instance,
        Player player,
        ref IEnumerable<CardModel> __result
    )
    {
        // GetPossibleCards 也被攻击药水、转换和其他战斗生成使用。
        // 只有 CardFactory.CreateForReward 的调用链才应用“奖励只出蛊虫”规则。
        if (RewardCandidateQueryDepth.Value <= 0)
        {
            return;
        }

        CardModel[] originalCandidates = __result.ToArray();
        IEnumerable<CardModel> candidates = originalCandidates;

        // 默认奖励已经直接读取角色蛊虫池，不再重复拼接。AllStar、
        // Kaleidoscope 等效果会显式替换卡池；此时才补回已解锁蛊牌，
        // 并重放原始过滤器，避免旧逻辑绕过类型或稀有度限制。
        if (player.Character is GuZhenRenCharacter &&
            originalCandidates.All(static card =>
                card.Pool is not GuZhenRenGuCardPool
            ))
        {
            IEnumerable<CardModel> fallback =
                GetUnlockedGuCards(player);

            if (__instance.CardPoolFilter is { } filter)
            {
                fallback = fallback.Where(filter);
            }

            candidates = candidates.Concat(fallback);
        }

        __result = candidates
            .GroupBy(card => card.Id)
            .Select(group => group.First())
            .Where(card =>
                GuZhenRenCardRules.CanAppearInCardReward(
                    player,
                    card
                ) &&
                GuZhenRenCardRules.CanOfferWithGuReplacement(
                    player.RunState,
                    player,
                    card
                )
            )
            .ToArray();
    }

    private static IEnumerable<CardModel> GetUnlockedGuCards(
        Player player
    )
    {
        return ModelDb
            .CardPool<GuZhenRenGuCardPool>()
            .GetUnlockedCards(
                player.UnlockState,
                player.RunState.CardMultiplayerConstraint
            );
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
            !GuZhenRenCardRules.CanOfferWithGuReplacement(
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
        ref IEnumerable<CardCreationResult> __result,
        out RewardQueryScope __state
    )
    {
        __state = new RewardQueryScope(
            RewardCandidateQueryDepth.Value
        );
        RewardCandidateQueryDepth.Value++;

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

    private static void CreateForRewardPostfix(
        RewardQueryScope __state
    )
    {
        __state.Restore();
    }

    private static Exception? CreateForRewardFinalizer(
        Exception? __exception,
        RewardQueryScope __state
    )
    {
        __state.Restore();
        return __exception;
    }

    private static bool TransformationOptionsPrefix(
        CardModel original,
        IEnumerable<CardModel> originalOptions,
        bool isInCombat,
        ref CardModel[] __result
    )
    {
        if (original.Owner.Character is not GuZhenRenCharacter ||
            original.Pool is not (
                GuZhenRenCardPool or GuZhenRenGuCardPool
            ))
        {
            return true;
        }

        TransformationPlan? plan =
            ActiveTransformationPlan.Value;

        IReadOnlySet<CardModel> ignoredOriginals =
            plan?.IgnoredOriginals ??
            new HashSet<CardModel> { original };

        IEnumerable<CardModel> candidates = originalOptions;

        if (original.Pool is GuZhenRenCardPool)
        {
            candidates = candidates.Where(card =>
                card is GuZhenRenStrike or GuZhenRenDefend
            );
        }
        else
        {
            candidates = candidates.Where(card =>
                card.Rarity is CardRarity.Common or
                    CardRarity.Uncommon or CardRarity.Rare
            );
        }

        if (isInCombat)
        {
            candidates = candidates.Where(card =>
                card.CanBeGeneratedInCombat
            );
        }

        candidates = CardFactory.FilterForPlayerCount(
            original.Owner.RunState,
            candidates.Where(card => card.Id != original.Id)
        );

        __result = candidates
            .Where(replacement =>
                GuZhenRenCardRules.CanUseAsTransformationResult(
                    original,
                    replacement,
                    ignoredOriginals,
                    plan?.PlannedAdditions
                )
            )
            .GroupBy(card => card.Id)
            .Select(group => group.First())
            .OrderBy(card => card.Id.ToString(), StringComparer.Ordinal)
            .ToArray();

        return false;
    }

    /// <summary>
    /// 原版单张随机转换对空结果直接调用 First()。最终仍无候选时保留
    /// 原牌并返回失败结果，使事件继续收尾而不会软锁。
    /// </summary>
    private static bool TransformToRandomPrefix(
        CardModel original,
        ref Task<CardPileAddResult> __result
    )
    {
        if (original.Owner.Character is not GuZhenRenCharacter ||
            original.Pool is not (
                GuZhenRenCardPool or GuZhenRenGuCardPool
            ))
        {
            return true;
        }

        bool hasCandidate = CardFactory
            .GetDefaultTransformationOptions(
                original,
                original.CombatState != null
            )
            .Any();

        if (hasCandidate)
        {
            return true;
        }

        Entry.Logger.Warn(
            $"{original.Id} 没有合法转换候选，已保留原牌并结束转换。"
        );
        CardPile? pile = original.Pile;
        __result = Task.FromResult(
            new CardPileAddResult
            {
                success = false,
                cardAdded = original,
                oldPile = pile,
                targetPile = pile?.Type ?? PileType.None,
                modifyingModels = null,
            }
        );
        return false;
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
         * 蛊虫：完全按当前转数重建本模组关键词。
         * GetKeywordsWithSources 返回的是实例 _keywords 快照，卡牌
         * 转数变化（升转/合练/读档/克隆）后不会自动同步，低转卡
         * 可能残留高转才有的词（如仙蛊）。先清掉全部本模组词，
         * 再按当前转数重新加入。仅处理真正蛊虫（AbstractGuWormCard），
         * 本命蛊等其他 IGuWormCard 保持原有清理逻辑。
         */
        if (__instance is AbstractGuWormCard guWorm)
        {
            keywords.RemoveWhere(
                GuZhenRenKeywords.OwnedKeywords.Contains
            );
            foreach (CardKeyword keyword in
                     AbstractGuWormCard.GetDisplayKeywordsFor(guWorm))
            {
                keywords.Add(keyword);
            }
        }

        /*
         * 动态关键词可能在 MutableClone/读档过程中被复制进卡牌本地集合。
         * 因此每次都先清除已经废弃的展示关键词和旧仙蛊展示，再由
         * 当前保存状态重建。旧存档与多人快照不会重新显示转数、
         * 蛊虫、资源、卡牌或 Power 已经能够说明的重复提示。
         */
        keywords.ExceptWith(
            GuZhenRenKeywords.RemovedDisplayKeywords
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

        __result = keywords;
    }

    private sealed class RewardQueryScope
    {
        private readonly int _previousDepth;
        private int _restored;

        internal RewardQueryScope(int previousDepth)
        {
            _previousDepth = previousDepth;
        }

        internal void Restore()
        {
            if (Interlocked.Exchange(ref _restored, 1) != 0)
            {
                return;
            }

            RewardCandidateQueryDepth.Value = _previousDepth;
        }
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

    /// <summary>
    /// 登记 LocString → 宿主牌 的映射，供 GetFormattedText 追加文本。
    /// </summary>
    private static void DescriptionRegisterPostfix(
        CardModel __instance,
        ref LocString __result
    )
    {
        if (__result == null ||
            !XueDaoParasiteSystem.HasParasite(__instance))
        {
            return;
        }

        DescriptionOwners.Remove(__result);
        DescriptionOwners.Add(__result, __instance);
    }

    /// <summary>
    /// 所有卡面/牌堆/预览描述渲染的最终出口：对已登记的有寄生宿主牌，
    /// 在格式化文本后追加动态效果行。ConditionalWeakTable 弱引用保证
    /// 无泄漏；无寄生或未登记的 LocString 立即返回。
    /// </summary>
    private static void GetFormattedTextPostfix(
        LocString __instance,
        ref string __result
    )
    {
        if (!DescriptionOwners.TryGetValue(
                __instance,
                out CardModel? card
            ))
        {
            return;
        }

        string? dynamicText =
            XueDaoParasiteSystem
                .GetHostCardDynamicText(card);
        if (string.IsNullOrEmpty(dynamicText))
        {
            return;
        }

        // Card text uses the game's named [purple] BBCode tag.  Older
        // localization files used Godot's raw [color=#…] tag, which is left
        // as literal text by the card renderer. Normalize both forms and
        // guarantee the parasite line is rendered as purple.
        dynamicText = dynamicText
            .Replace("[color=#B388FF]", "[purple]", StringComparison.OrdinalIgnoreCase)
            .Replace("[/color]", "[/purple]", StringComparison.OrdinalIgnoreCase);
        if (!dynamicText.Contains("[purple]", StringComparison.OrdinalIgnoreCase))
        {
            dynamicText = $"[purple]{dynamicText}[/purple]";
        }

        if (__result.EndsWith(
                dynamicText,
                StringComparison.Ordinal
            ))
        {
            return;
        }

        __result = $"{__result}\n{dynamicText}";
    }
}
