using System.Reflection;

using Godot;

using GuZhenRen.Aperture;
using GuZhenRen.Cards.ImmortalEssence;
using GuZhenRen.Cards.ShaZhao;
using GuZhenRen.Characters;
using GuZhenRen.Combat;
using GuZhenRen.Multiplayer;
using GuZhenRen.Relics;

using HarmonyLib;

using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib;
using STS2RitsuLib.CardPiles.Nodes;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interactions.RightClick;
using STS2RitsuLib.Utils;

namespace GuZhenRen.Cards;

/// <summary>
/// 杀招推演与主动解体。
///
/// 推演入口为“杀招推演”系统牌（不绑定任何牌堆的右键操作）；
/// 主动解体绑定在手牌中已绑定材料的杀招卡上（右键，1 费，
/// 材料返回并额外增加 1 回合冷却，随后杀招消耗）。牌堆点击只负责
/// 发起请求；实际选择、扣费和结算由 RitsuLib 的托管联机行动在所有
/// 端同步执行，不要求玩家记忆材料配方。
/// </summary>
internal static class ShaZhaoTuiYanSystem
{
    private const string HarmonyId =
        Entry.ModId + ".ShaZhaoTuiYanPileRightClick";

    private const string BindingLocalId =
        "sha_zhao_tui_yan_from_gu_pile";

    private const string TriggerMetadata =
        Entry.ModId + ":sha_zhao_tui_yan_from_gu_pile";

    private const int ActivationEnergyCost = 1;
    private const int BacklashDamage = 5;

    private static IDisposable? _rightClickBinding;
    private static bool _initialized;

    internal static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        // 0.8.0 杀招系统：推演入口改为“杀招推演”系统牌，
        // 不再注册蛊恢复堆的右键推演。
        //
        // 主动解体：右键手牌中的任意已绑定杀招（包括瞬发/消耗型），
        // 支付 1 费，材料返回并额外增加 1 回合恢复，随后杀招消耗。
        _rightClickBinding = ModRightClickRegistry.Register<CardModel>(
            Entry.ModId,
            "sha_zhao_dismantle",
            static ctx => ctx.Model
                    is AbstractShaZhaoCard shaZhao
                && shaZhao.HasBoundMaterials
                && ctx.Player.PlayerCombatState is { } combatState
                && combatState.Energy >= 1
                && combatState.Hand.Cards.Contains(shaZhao),
            static ctx => ctx.Player is { } player
                && ctx.PlayerChoiceContext is { } choiceContext
                    ? ((AbstractShaZhaoCard)ctx.Model)
                        .TryDismantleAsync(choiceContext, player)
                    : Task.CompletedTask,
            priority: 100
        );

        Harmony harmony = new(HarmonyId);
        MethodInfo? removeFromCombat = AccessTools.Method(
            typeof(CardPileCmd),
            nameof(CardPileCmd.RemoveFromCombat),
            [typeof(CardModel), typeof(bool)]
        );
        if (removeFromCombat != null &&
            removeFromCombat.ReturnType == typeof(Task))
        {
            harmony.Patch(
                removeFromCombat,
                postfix: new HarmonyMethod(
                    typeof(ShaZhaoTuiYanSystem),
                    nameof(RemoveFromCombatPostfix)
                )
            );
        }
        else
        {
            Entry.Logger.Warn(
                "[杀招绑定] 未找到兼容的 CardPileCmd.RemoveFromCombat，" +
                "异常移出兜底未挂载。"
            );
        }

        MethodInfo? afterCombatEnd = AccessTools.Method(
            typeof(Hook),
            nameof(Hook.AfterCombatEnd)
        );
        if (afterCombatEnd != null)
        {
            harmony.Patch(
                afterCombatEnd,
                prefix: new HarmonyMethod(
                    typeof(ShaZhaoTuiYanSystem),
                    nameof(AfterCombatEndPrefix)
                )
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
            _rightClickBinding?.Dispose();
            _rightClickBinding = null;
            _initialized = false;
        }
    }

    private static void RemoveUncommittedResult(
        AbstractShaZhaoCard shaZhao
    )
    {
        var combatState = shaZhao.CombatState;
        shaZhao.RemoveFromState();
        combatState?.RemoveCard(shaZhao);
    }

    /// <summary>
    /// 失败只影响发起玩家的界面；托管行动仍在所有端以相同状态结束。
    /// 材料尚未移动，因此无需补偿命令，直接保留在蛊牌堆。
    /// </summary>
    private static void ShowSynthesisFailure(
        Player player,
        string reason
    )
    {
        Entry.Logger.Info(
            $"杀招推演失败（{reason}）：未消耗费用，材料保留在蛊牌堆。"
        );

        if (!LocalContext.IsMe(player))
        {
            return;
        }

        NModalContainer? container = NModalContainer.Instance;
        if (container == null || container.OpenModal != null)
        {
            return;
        }

        LocString title = new(
            "cards",
            "GU_ZHEN_REN_PERSONAL_SHA_ZHAO_SYNTHESIS.failureTitle"
        );
        LocString body = new(
            "cards",
            $"GU_ZHEN_REN_PERSONAL_SHA_ZHAO_SYNTHESIS.{reason}"
        );

        NErrorPopup? popup = NErrorPopup.Create(
            title,
            body,
            cancel: null,
            showReportBugButton: false
        );

        if (popup != null)
        {
            container.Add(popup);
        }
    }

    /// <summary>
    /// 成功推演费用 = 所有材料有效元气消耗的算术平均值。
    /// 元气是整数资源，因此非整数结果统一向上取整。
    /// 优先读取 RitsuLib 已解析的元气支付计划；没有声明次级费用的旧蛊牌
    /// 则使用当前原生能量消耗兼容计算。
    /// </summary>
    private static int CalculateShaZhaoYuanQiCost(
        Player player,
        IReadOnlyList<CardModel> materials
    )
    {
        if (materials.Count == 0)
        {
            return 0;
        }

        long totalMaterialCost = materials.Sum(card =>
            (long)Math.Max(0, GetMaterialYuanQiCost(player, card))
        );

        long roundedUpAverage =
            (totalMaterialCost + materials.Count - 1) /
            materials.Count;
        return (int)Math.Min(int.MaxValue, roundedUpAverage);
    }

    private static int GetMaterialYuanQiCost(
        Player player,
        CardModel card
    )
    {
        SecondaryResourcePaymentPlan plan =
            SecondaryResourcePaymentResolver.Plan(
                card,
                source: card
            );

        SecondaryResourcePaymentLine[] yuanQiLines =
            plan.Lines
                .Where(line => string.Equals(
                    line.ResourceId,
                    YuanQiSystem.ResourceId,
                    StringComparison.OrdinalIgnoreCase
                ))
                .ToArray();

        if (yuanQiLines.Length > 0)
        {
            long resolvedCost = yuanQiLines.Sum(line =>
                (long)(line.BlocksPlay || line.Activated
                    ? line.CostsX
                        ? line.AmountToSpend
                        : line.Cost
                    : 0)
            );

            return (int)Math.Min(
                int.MaxValue,
                Math.Max(0L, resolvedCost)
            );
        }

        return Math.Max(
            0,
            card.EnergyCost.CostsX
                ? player.PlayerCombatState?.Energy ?? 0
                : card.EnergyCost.GetWithModifiers(
                    CostModifiers.All
                )
        );
    }

    private static async Task<(
        Type? TargetCardType,
        List<CardModel> Materials
    )>
        SelectTargetAndMaterialsAsync(
            MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext
                choiceContext,
            Player player,
            CardPile guPile
    )
    {
        // 杀招推演材料可从蛊手牌与蛊存放牌堆选择。
        // 蛊恢复/冷却牌堆与蛊封存堆不参与推演候选。
        CardModel[] availableMaterials = guPile.Cards
            .Concat(GuCardPileSystem.StoragePileType.GetPile(player).Cards)
            .Distinct()
            .Where(IsEligibleMaterial)
            .ToArray();

        Type[] craftableResultTypes =
            ShaZhaoRecipeRegistry
                .GetCraftableResultTypes(availableMaterials)
                .ToArray();

        if (craftableResultTypes.Length == 0)
        {
            return (null, []);
        }

        CardModel[] targetPreviews = craftableResultTypes
            .Select(resultType =>
            {
                CardModel preview =
                    ModelDb
                        .CardPool<GuZhenRenShaZhaoCardPool>()
                        .AllCards
                        .Single(card => card.GetType() == resultType)
                        .ToMutable();

                // NSimpleCardSelectScreen 在战斗中会从第一张候选牌的 Owner
                // 初始化战斗牌堆。卡池模板的 ToMutable() 不会自动绑定玩家，
                // 因此所有用于战斗选卡的预览牌都必须显式设置 Owner。
                preview.Owner = player;
                return preview;
            })
            .ToArray();

        LocString targetPrompt = new(
            "static_hover_tips",
            GuCardPileSystem.PileId + ".targetSelectionPrompt"
        );
        CardModel? target =
            (
                await CardSelectCmd.FromSimpleGrid(
                    choiceContext,
                    targetPreviews,
                    player,
                    new CardSelectorPrefs(targetPrompt, 1)
                    {
                        Cancelable = true,
                        // 即使当前只能推演出一张杀招，也必须进入选择界面，
                        // 由玩家明确点击选择，而不是被选卡命令自动确认。
                        RequireManualConfirmation = true,
                        PretendCardsCanBePlayed = true,
                    }
                )
            ).FirstOrDefault();

        if (target == null)
        {
            return (null, []);
        }

        Type targetCardType = target.GetType();
        IReadOnlyList<Type> materialTypes =
            ShaZhaoRecipeRegistry.GetMaterialTypesForResult(
                targetCardType
            );
        ShaZhaoRecipeRegistry.GetMaterialCountRangeForResult(
            targetCardType,
            out int minimumMaterialCount,
            out int maximumMaterialCount
        );

        CardModel[] choices = availableMaterials
            .Where(card =>
                materialTypes.Contains(card.GetType())
            )
            .Select((card, index) => (card, index))
            .OrderBy(item => item.card.Id.ToString(), StringComparer.Ordinal)
            .ThenByDescending(item =>
                item.card is IGuRankProvider rankProvider
                    ? rankProvider.GuRank
                    : 0
            )
            .ThenBy(item => item.card.CurrentUpgradeLevel)
            .ThenBy(item =>
                item.card.Enchantment?.Id.ToString() ?? string.Empty,
                StringComparer.Ordinal
            )
            .ThenBy(item =>
                GuZhenRenDeterminism.GetCardNetworkId(item.card)
            )
            .ThenBy(item => item.index)
            .Select(item => item.card)
            .ToArray();

        if (choices.Length < minimumMaterialCount)
        {
            return (targetCardType, []);
        }

        // 目标杀招确定后，如果蛊手牌与蛊存放牌堆中只存在唯一一组
        // 合法材料实例，则直接使用，不再打开材料选择界面。
        // 若同名蛊有多个实例，或存在多条当前都可完成的替代配方，
        // 则仍进入材料选择界面，让玩家决定具体使用哪一组材料。
        if (TryGetUniqueMaterialSelection(
                choices,
                targetCardType,
                minimumMaterialCount,
                maximumMaterialCount,
                out List<CardModel> uniqueMaterials
            ))
        {
            return (targetCardType, uniqueMaterials);
        }

        LocString materialPrompt = new(
            "static_hover_tips",
            GuCardPileSystem.PileId + ".selectionPrompt"
        );
        materialPrompt.Add("TargetName", target.Title);

        List<CardModel> selected =
            (
                await CardSelectCmd.FromSimpleGrid(
                    choiceContext,
                    choices,
                    player,
                    new CardSelectorPrefs(
                        materialPrompt,
                        minimumMaterialCount,
                        maximumMaterialCount
                    )
                    {
                        Cancelable = true,
                        RequireManualConfirmation =
                            minimumMaterialCount != maximumMaterialCount,
                        PretendCardsCanBePlayed = true,
                    }
                )
            )
            .ToList();

        if (selected.Count < minimumMaterialCount ||
            selected.Count > maximumMaterialCount)
        {
            selected.Clear();
        }

        return (targetCardType, selected);
    }

    /// <summary>
    /// 判断指定杀招是否只有唯一一组合法材料实例。
    /// 候选范围已经限定为蛊手牌与蛊存放牌堆。
    /// 直接枚举组合可以正确处理同类型蛊的多个不同实例、
    /// 替代配方以及需要重复材料类型的配方。
    /// </summary>
    private static bool TryGetUniqueMaterialSelection(
        IReadOnlyList<CardModel> choices,
        Type targetCardType,
        int minimumMaterialCount,
        int maximumMaterialCount,
        out List<CardModel> selected
    )
    {
        selected = [];

        List<CardModel>? uniqueSelection = null;
        bool foundMultiple = false;
        List<CardModel> current = [];

        void Search(int startIndex, int remaining)
        {
            if (foundMultiple)
            {
                return;
            }

            if (remaining == 0)
            {
                if (!ShaZhaoRecipeRegistry.HasMatchingRecipe(
                        current,
                        targetCardType
                    ))
                {
                    return;
                }

                if (uniqueSelection != null)
                {
                    foundMultiple = true;
                    return;
                }

                uniqueSelection = [.. current];
                return;
            }

            for (
                int index = startIndex;
                index <= choices.Count - remaining;
                index++
            )
            {
                current.Add(choices[index]);
                Search(index + 1, remaining - 1);
                current.RemoveAt(current.Count - 1);

                if (foundMultiple)
                {
                    return;
                }
            }
        }

        int minimum = Math.Max(0, minimumMaterialCount);
        int maximum = Math.Min(
            choices.Count,
            Math.Max(minimum, maximumMaterialCount)
        );

        for (
            int materialCount = minimum;
            materialCount <= maximum && !foundMultiple;
            materialCount++
        )
        {
            Search(0, materialCount);
        }

        if (foundMultiple || uniqueSelection == null)
        {
            return false;
        }

        selected = uniqueSelection;
        return true;
    }

    private static bool IsEligibleMaterial(CardModel card)
    {
        if (card is not IGuWormCard ||
            !IsInEligibleMaterialPile(card) ||
            GuSealSystem.IsSealed(card) ||
            card.Keywords.Contains(CardKeyword.Unplayable))
        {
            return false;
        }

        // 蛊手牌与蛊存放牌堆中的材料都必须仍可催动
        // （未耗尽次数、未被封存）。
        return GuCardUsageRules.CanUse(card);
    }

    /// <summary>
    /// 杀招推演材料可来自蛊手牌或蛊存放牌堆。
    /// 蛊恢复/冷却牌堆与蛊封存堆不适用。
    /// </summary>
    private static bool IsInEligibleMaterialPile(CardModel card)
    {
        return card.Pile?.Type == GuCardPileSystem.PileType ||
            card.Pile?.Type == GuCardPileSystem.StoragePileType;
    }

    /// <summary>
    private static async Task ResolveSuccessfulRecipe(
        MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext
            choiceContext,
        Player player,
        IReadOnlyList<CardModel> selectedCards,
        AbstractShaZhaoCard shaZhao
    )
    {
        using (ShaZhaoSynthesisScope.Enter())
        {
            foreach (CardModel material in selectedCards)
            {
                await CardExhaustCompat.ExhaustAsync(
                    choiceContext,
                    material
                );
            }
        }

        bool addedToHand =
            await GuCardPileSystem.AddGeneratedCardToHand(
                shaZhao,
                player
            );

        // 多人或满手牌时绝不能让已创建并登记的杀招凭空丢失。
        if (!addedToHand)
        {
            await CardPileCmd.AddGeneratedCardToCombat(
                shaZhao,
                PileType.Discard,
                player
            );
        }
    }

    private static async Task ResolveFailedRecipe(
        MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext
            choiceContext,
        Player player,
        IReadOnlyList<CardModel> selectedCards,
        PlayerCombatState playerCombatState
    )
    {
        (CardModel card, int energyCost)[] materialCosts =
            selectedCards
                .Select(card =>
                    (
                        card,
                        energyCost: card.EnergyCost.CostsX
                            ? 0
                            : Math.Max(
                                0,
                                card.EnergyCost.GetWithModifiers(
                                    CostModifiers.All
                                )
                            )
                    )
                )
                .ToArray();

        int totalMaterialCost =
            materialCosts.Sum(item => item.energyCost);

        if (playerCombatState.Energy < totalMaterialCost)
        {
            await CreatureCmd.Damage(
                choiceContext,
                player.Creature,
                BacklashDamage,
                ValueProp.Unblockable | ValueProp.Unpowered,
                dealer: null,
                cardSource: null,
                cardPlay: null
            );
            return;
        }

        foreach ((CardModel card, int energyCost) material in
                 materialCosts)
        {
            if (material.energyCost > 0)
            {
                await material.card.SpendEnergy(
                    material.energyCost
                );
            }
        }

        foreach ((CardModel card, int energyCost) material in
                 materialCosts)
        {
            Creature? target =
                ResolveAutoPlayTarget(player, material.card);

            try
            {
                bool costsX = material.card.EnergyCost.CostsX;

                if (costsX)
                {
                    material.card.EnergyCost.CapturedXValue = 0;
                }

                await CardCmd.AutoPlay(
                    choiceContext,
                    material.card,
                    target!,
                    skipXCapture: costsX
                );
            }
            catch (ArgumentNullException) when (target == null)
            {
                // 前序材料可能消灭最后一个合法单体目标。
            }
        }
    }

    private static Creature? ResolveAutoPlayTarget(
        Player player,
        CardModel card
    )
    {
        var combatState =
            card.CombatState ?? player.Creature.CombatState;

        if (combatState == null)
        {
            return card.IsValidTarget(player.Creature)
                ? player.Creature
                : null;
        }

        Creature[] validEnemyTargets =
            GuZhenRenDeterminism.OrderCreatures(
                combatState.HittableEnemies.Where(
                    card.IsValidTarget
                )
            );

        if (validEnemyTargets.Length > 0)
        {
            return RitsuLibFramework
                .GetModPlayerRng(
                    player,
                    Entry.ModId,
                    "sha_zhao_tui_yan/auto_target"
                )
                .NextItem(validEnemyTargets);
        }

        return card.IsValidTarget(player.Creature)
            ? player.Creature
            : null;
    }

    // =================================================================
    //  0.8.0 杀招系统：材料封装与返还
    // =================================================================

    private static readonly SavedAttachedState<CardModel, string>
        MaterialBoundShaZhaoState = new(
            "lu_gu_zhen_ren.sha_zhao.material_bound_sha_zhao",
            static () => string.Empty
        );

    internal enum ShaZhaoBindingFinalizeReason
    {
        Completed,
        Dismantled,
        AbnormalRemoval,
        CombatEnd,
    }

    /// <summary>
    /// 材料蛊是否已被某张杀招封装。
    /// 封装期间不能催动、不能恢复、不能参与其他杀招。
    /// </summary>
    public static bool IsMaterialSealed(CardModel card)
    {
        return GuSealSystem.IsShaZhaoMaterialSealed(card);
    }

    /// <summary>
    /// 仅供 GuSealSystem 兼容旧版 QuickSL；不得作为通用封存判断入口。
    /// </summary>
    internal static bool HasMaterialBindingState(CardModel card) =>
        card is IGuWormCard &&
        MaterialBoundShaZhaoState[card].Length > 0;

    internal static string GetMaterialBindingTitle(CardModel material)
    {
        string boundId = MaterialBoundShaZhaoState[material];
        if (boundId.Length == 0)
        {
            return string.Empty;
        }

        AbstractShaZhaoCard? shaZhao = material.Owner?
            .PlayerCombatState?
            .AllCards
            .OfType<AbstractShaZhaoCard>()
            .FirstOrDefault(card => string.Equals(
                card.Id.ToString(),
                boundId,
                StringComparison.Ordinal
            ));
        return shaZhao?.Title ?? boundId;
    }

    internal static async Task MarkMaterialSealedAsync(
        CardModel material,
        CardModel shaZhao
    )
    {
        if (GuSealSystem.IsSealed(material))
        {
            throw new InvalidOperationException(
                $"蛊虫 {material.Id} 已因 " +
                $"{GuSealSystem.GetSealReason(material)} 封存，" +
                "不能再次作为杀招材料。"
            );
        }

        MaterialBoundShaZhaoState[material] =
            shaZhao.Id.ToString();
        GuSealSystem.SealAsShaZhaoMaterial(material);

        // 材料从蛊手牌移入蛊封存堆（可见牌堆，位于原版
        // 消耗牌堆上方）：使用原版消耗牌动画（飞行动画）移动；
        // 恢复流程会跳过封装材料，因此它既不能催动也不会自动恢复。
        Player player = material.Owner;
        CardPile materialPile =
            GuCardPileSystem.GuSealedPileType
                .GetPile(player);
        if (!ReferenceEquals(material.Pile, materialPile))
        {
            await GuCardPileSystem.MoveCardToPileAsync(
                material,
                GuCardPileSystem.GuSealedPileType,
                skipVisuals: false
            );
        }
    }

    /// <summary>
    /// 杀招绑定的唯一收口。正常用完、主动解体或异常移出战斗都会
    /// 让材料重新开始完整冷却并统一额外 +1；战斗结束只清理战斗状态。
    /// </summary>
    public static async Task FinalizeShaZhaoBindingAsync(
        AbstractShaZhaoCard shaZhao,
        Player player,
        ShaZhaoBindingFinalizeReason reason
    )
    {
        if (!shaZhao.HasBoundMaterials ||
            shaZhao.MaterialsSealedPermanently)
        {
            return;
        }

        IReadOnlyList<CardModel> materials =
            shaZhao.BoundMaterials;

        if (reason == ShaZhaoBindingFinalizeReason.CombatEnd)
        {
            foreach (CardModel material in materials)
            {
                MaterialBoundShaZhaoState[material] = string.Empty;
                GuSealSystem.ClearSeal(
                    material,
                    GuSealReason.ShaZhaoMaterial
                );
            }
            shaZhao.ClearBoundMaterials();
            return;
        }

        foreach (CardModel material in materials)
        {
            await UnsealMaterialAsync(
                material,
                player
            );
        }

        shaZhao.ClearBoundMaterials();
    }

    /// <summary>
    /// 战斗结束兜底：在原版清空战斗牌之前解除所有普通杀招绑定。
    /// </summary>
    private static void FinalizeAllBindingsForCombatEnd(Player player)
    {
        AbstractShaZhaoCard[] shaZhaoCards = player.PlayerCombatState?
            .AllCards
            .OfType<AbstractShaZhaoCard>()
            .Where(static card =>
                card.HasBoundMaterials &&
                !card.MaterialsSealedPermanently
            )
            .ToArray() ?? [];

        foreach (AbstractShaZhaoCard shaZhao in shaZhaoCards)
        {
            foreach (CardModel material in shaZhao.BoundMaterials)
            {
                MaterialBoundShaZhaoState[material] = string.Empty;
                GuSealSystem.ClearSeal(
                    material,
                    GuSealReason.ShaZhaoMaterial
                );
            }
            shaZhao.ClearBoundMaterials();
        }
    }

    private static async Task UnsealMaterialAsync(
        CardModel material,
        Player player
    )
    {
        MaterialBoundShaZhaoState[material] =
            string.Empty;
        GuSealSystem.ClearSeal(
            material,
            GuSealReason.ShaZhaoMaterial
        );

        // 送回蛊恢复堆（飞行动画），并从当前回合开始从零强制恢复：
        // 无视封存前已有的恢复进度，完整恢复周期重新计算；
        // 所有正常完成/解体/异常移出统一额外延后 1 回合。
        CardPile recoveryPile =
            GuCardPileSystem.RecoveryPileType
                .GetPile(player);
        if (!ReferenceEquals(material.Pile, recoveryPile))
        {
            await GuCardPileSystem.MoveCardToPileAsync(
                material,
                GuCardPileSystem.RecoveryPileType,
                skipVisuals: false
            );
        }

        int currentTurn =
            player.PlayerCombatState?.TurnNumber ?? 0;
        GuCardUsageRules.ResetRecovery(
            material,
            currentTurn,
            extraTurns: 1
        );
    }

    private static void RemoveFromCombatPostfix(
        object[] __args,
        ref Task __result
    )
    {
        AbstractShaZhaoCard? shaZhao = __args
            .OfType<AbstractShaZhaoCard>()
            .FirstOrDefault();
        if (shaZhao == null || !shaZhao.HasBoundMaterials)
        {
            return;
        }

        Player player = shaZhao.Owner;
        __result = AwaitRemovalAndFinalizeAsync(
            __result,
            shaZhao,
            player
        );
    }

    private static async Task AwaitRemovalAndFinalizeAsync(
        Task removalTask,
        AbstractShaZhaoCard shaZhao,
        Player player
    )
    {
        await removalTask;
        if (shaZhao.HasBoundMaterials)
        {
            await FinalizeShaZhaoBindingAsync(
                shaZhao,
                player,
                ShaZhaoBindingFinalizeReason.AbnormalRemoval
            );
        }
    }

    private static void AfterCombatEndPrefix(
        IRunState runState,
        CombatRoom room
    )
    {
        foreach (Player player in runState.Players)
        {
            FinalizeAllBindingsForCombatEnd(player);
        }
    }

    /// <summary>
    /// 打出“杀招推演”系统牌的推演入口。
    ///
    /// 成功返回 true（推演牌应消耗），且只有成功收口时才实际消耗元气；
    /// 取消、配方无效、资源不足或其他正常失败返回 false，
    /// 推演牌回到手牌且不消耗元气。
    /// </summary>
    public static async Task<bool> DeriveFromCardAsync(
        PlayerChoiceContext choiceContext,
        Player player,
        CardModel derivationCard
    )
    {
        PlayerCombatState? playerCombatState =
            player.PlayerCombatState;
        if (playerCombatState == null)
        {
            return false;
        }

        CardPile guPile =
            GuCardPileSystem.PileType.GetPile(player);

        (
            Type? targetCardType,
            List<CardModel> selectedCards
        ) = await SelectTargetAndMaterialsAsync(
                choiceContext,
                player,
                guPile
            );

        if (targetCardType == null || selectedCards.Count == 0)
        {
            return false;
        }

        if (!selectedCards.All(card =>
                ReferenceEquals(card.Owner, player) &&
                IsInEligibleMaterialPile(card) &&
                IsEligibleMaterial(card)
            ))
        {
            ShowSynthesisFailure(player, "stateChanged");
            return false;
        }

        if (!ShaZhaoRecipeRegistry.HasMatchingRecipe(
                selectedCards,
                targetCardType
            ))
        {
            ShowSynthesisFailure(player, "invalidRecipe");
            return false;
        }

        // 杀招转数 = 材料最高转数；六转及以上为仙道杀招。
        // 推演牌成功时一律支付 1 点普通能量；凡道杀招额外支付材料
        // 元气费用的向上取整平均值，仙道杀招额外支付最高转对应仙元。
        // 无论凡道仙道，推演出的杀招首次打出均免费。
        int materialMaxRank = selectedCards.Count == 0
            ? 0
            : selectedCards.Max(
                static card =>
                    AbstractShaZhaoCard.GetMaterialRank(card)
            );
        bool isImmortalShaZhao =
            materialMaxRank >= ApertureProgression.ImmortalRank;

        int xianYuanCost =
            isImmortalShaZhao
                ? ImmortalEssenceSystem.GetActivationCost(materialMaxRank)
                : 0;
        int yuanQiCost = 0;

        if (playerCombatState.Energy < ActivationEnergyCost)
        {
            ShowSynthesisFailure(player, "stateChanged");
            return false;
        }

        if (isImmortalShaZhao)
        {
            if (ImmortalEssenceSystem.GetAvailableUnits(player) <
                xianYuanCost)
            {
                ShowSynthesisFailure(player, "insufficientXianYuan");
                return false;
            }
        }
        else
        {
            yuanQiCost = CalculateShaZhaoYuanQiCost(
                player,
                selectedCards
            );

            if (SecondaryResourceCmd.Get(
                    player,
                    YuanQiSystem.ResourceId
                ) < yuanQiCost)
            {
                ShowSynthesisFailure(player, "insufficientResources");
                return false;
            }
        }

        if (!ShaZhaoRecipeRegistry.TryCreateResultForTarget(
                selectedCards,
                player,
                targetCardType,
                out AbstractShaZhaoCard? shaZhao
            ))
        {
            ShowSynthesisFailure(player, "creationFailed");
            return false;
        }

        if (isImmortalShaZhao)
        {
            // 仙道杀招：按转数扣仙元。
            bool spentXianYuan =
                await ImmortalEssenceSystem.SpendUnits(
                    player,
                    xianYuanCost
                );

            if (!spentXianYuan)
            {
                RemoveUncommittedResult(shaZhao);
                ShowSynthesisFailure(player, "insufficientXianYuan");
                return false;
            }
        }
        else
        {
            // 凡道杀招的元气这里只做过余额预检，不在此处实际扣除。
            // 实际扣费统一延后到整个推演流程成功收口，确保取消、失败、
            // 状态变化、创建失败或无法进入手牌等情况都不会消耗元气。
        }

        // “杀招推演”本身始终是一张 1 费普通牌。仅成功推演后扣除，
        // 取消、失败与资源不足均不收费。
        await PlayerCmd.LoseEnergy(
            ActivationEnergyCost,
            player
        );

        // 所有杀招首次打出免费（打出后恢复原费；次数型杀招视为第一次打出）。
        shaZhao.EnergyCost.SetUntilPlayed(0);

        // 材料封装：材料移入隐藏材料区并绑定到杀招。
        await shaZhao.BindMaterialsAsync(selectedCards);
        // 先封存完全部材料，再统一补一次蛊手牌，避免多次动画与同步交错。
        await GuCardPileSystem.RefillGuHandAsync(player);

        // 材料永久封存型杀招（如万我）：推演完成时立即解除材料绑定，
        // 材料保持封存、不参与杀招消耗/解体/战斗结束兜底的返还。
        if (shaZhao.MaterialsSealedPermanently)
        {
            shaZhao.ClearBoundMaterials();
        }

        // 登记每场推演次数；八至九转补发第二张推演牌。
        await ApertureSystem.RegisterShaZhaoDerivationAsync(player);

        // 杀招加入普通手牌；杀招本体仍占容量，只有“杀招推演”系统牌容量豁免。
        bool addedToHand =
            await HandCapacityExemptionPatch
                .AddGeneratedShaZhaoToHandAsync(shaZhao, player);
        if (!addedToHand)
        {
            await FinalizeShaZhaoBindingAsync(
                shaZhao,
                player,
                ShaZhaoBindingFinalizeReason.AbnormalRemoval
            );
            RemoveUncommittedResult(shaZhao);
            throw new InvalidOperationException(
                $"推演杀招 {shaZhao.Id} 无法进入普通手牌。"
            );
        }

        // 只有整个推演流程已经成功完成，才实际消耗凡道杀招的元气。
        // 此处之前的所有取消/失败分支均不会执行 SecondaryResourceCmd.Spend。
        if (!isImmortalShaZhao && yuanQiCost > 0)
        {
            bool spentYuanQi =
                await SecondaryResourceCmd.Spend(
                    player,
                    YuanQiSystem.ResourceId,
                    yuanQiCost,
                    card: shaZhao,
                    source: shaZhao
                );

            if (!spentYuanQi)
            {
                // 前面已经做过余额预检，正常流程不应到这里失败。
                // 若资源在推演过程中被其他同步效果意外改变，明确抛错，
                // 避免把“未支付元气”的结果静默视为成功。
                throw new InvalidOperationException(
                    $"杀招推演已完成，但元气扣除失败：" +
                    $"需要 {yuanQiCost} 点元气。"
                );
            }
        }

        Entry.Logger.Info(
            $"[杀招推演] 成功推演 {shaZhao.GetType().Name}，" +
            $"消耗能量 {ActivationEnergyCost}、元气 {yuanQiCost}、" +
            $"仙元 {xianYuanCost}，材料 {selectedCards.Count} 张。"
        );

        return true;
    }
}
