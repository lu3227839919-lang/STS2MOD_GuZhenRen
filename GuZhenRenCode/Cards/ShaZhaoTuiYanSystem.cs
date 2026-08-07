using System.Reflection;

using Godot;

using GuZhenRen.Aperture;
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
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib;
using STS2RitsuLib.CardPiles.Nodes;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interactions.RightClick;
using STS2RitsuLib.Utils;

namespace GuZhenRen.Cards;

/// <summary>
/// 将“杀招推演”绑定到蛊恢复牌堆右键操作。
///
/// 牌堆点击只负责发起请求；实际选择、扣费和结算由 RitsuLib 的托管
/// 联机行动在所有端同步执行。推演先选择可制作的杀招，再从蛊存放牌堆
/// 中选择该杀招允许的材料，不要求玩家记忆材料配方。
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
            "GU_ZHEN_REN_SHA_ZHAO_SYNTHESIS.failureTitle"
        );
        LocString body = new(
            "cards",
            $"GU_ZHEN_REN_SHA_ZHAO_SYNTHESIS.{reason}"
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
    /// 成功推演费用 = 所有材料有效元气消耗的平均值 × 2。
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

        // 0.8.0 新公式：max(2, Σ材料元气 − 材料数量 + 1)
        // 单材料不再被重复计费；多材料获得少量组合折扣。
        int totalMaterialCost = materials.Sum(card =>
            Math.Max(0, GetMaterialYuanQiCost(player, card))
        );

        return Math.Max(
            2,
            totalMaterialCost - materials.Count + 1
        );
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
        CardModel[] availableMaterials = guPile.Cards
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
                ModelDb
                    .CardPool<GuZhenRenShaZhaoCardPool>()
                    .AllCards
                    .Single(card => card.GetType() == resultType)
                    .ToMutable()
            )
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

        CardModel[] choices = guPile.Cards
            .Where(card =>
                IsEligibleMaterial(card) &&
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

    private static bool IsEligibleMaterial(CardModel card)
    {
        return card is IGuWormCard &&
            card.Pile?.Type == GuCardPileSystem.PileType &&
            GuCardUsageRules.CanUse(card) &&
            !card.Keywords.Contains(CardKeyword.Unplayable);
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
            "gu_zhen_ren.sha_zhao.material_bound_sha_zhao",
            static () => string.Empty
        );

    /// <summary>
    /// 材料蛊是否已被某张杀招封装。
    /// 封装期间不能催动、不能恢复、不能参与其他杀招。
    /// </summary>
    public static bool IsMaterialSealed(CardModel card)
    {
        return card is IGuWormCard &&
            MaterialBoundShaZhaoState[card].Length > 0;
    }

    internal static async Task MarkMaterialSealedAsync(
        CardModel material,
        CardModel shaZhao
    )
    {
        MaterialBoundShaZhaoState[material] =
            shaZhao.Id.ToString();

        // 材料从蛊存放堆/恢复堆移入蛊封存堆（可见牌堆，位于原版
        // 消耗牌堆上方）：使用原版消耗牌动画（飞行动画）移动；
        // 恢复流程会跳过封装材料，因此它既不能催动也不会自动恢复。
        Player player = material.Owner;
        CardPile materialPile =
            GuCardPileSystem.MaterialPileType
                .GetPile(player);
        if (!ReferenceEquals(material.Pile, materialPile))
        {
            await GuCardPileSystem.MoveCardToPileAsync(
                material,
                GuCardPileSystem.MaterialPileType,
                skipVisuals: false
            );
        }
    }

    /// <summary>
    /// 解除封装并送还恢复：杀招正常消耗、主动解体时调用。
    /// 材料从蛊封存堆飞入蛊恢复堆，并从零强制恢复；
    /// 消耗牌杀招（Exhaust）使用后额外 +1 回合恢复。
    /// </summary>
    public static async Task ReleaseMaterialsAsync(
        PlayerChoiceContext choiceContext,
        AbstractShaZhaoCard shaZhao,
        Player player,
        bool extraRecoveryTurn
    )
    {
        IReadOnlyList<CardModel> materials =
            shaZhao.BoundMaterials;

        foreach (CardModel material in materials)
        {
            await UnsealMaterialAsync(
                material,
                shaZhao,
                player,
                extraRecoveryTurn
            );
        }

        shaZhao.ClearBoundMaterials();
        await Task.CompletedTask;
    }

    /// <summary>
    /// 战斗结束兜底：无条件解除全部杀招绑定并返还材料。
    /// </summary>
    public static void ReleaseMaterialsForCombatEnd(
        AbstractShaZhaoCard shaZhao,
        Player player
    )
    {
        foreach (CardModel material in shaZhao.BoundMaterials)
        {
            MaterialBoundShaZhaoState[material] =
                string.Empty;

            CardPile recoveryPile =
                GuCardPileSystem.RecoveryPileType
                    .GetPile(player);
            if (!ReferenceEquals(material.Pile, recoveryPile))
            {
                GuCardPileSystem.MoveCardToPile(
                    material,
                    recoveryPile
                );
            }

            int currentTurn =
                player.PlayerCombatState?.TurnNumber ?? 0;
            GuCardUsageRules.ScheduleRecovery(
                material,
                currentTurn
            );
        }

        shaZhao.ClearBoundMaterials();
    }

    private static async Task UnsealMaterialAsync(
        CardModel material,
        AbstractShaZhaoCard shaZhao,
        Player player,
        bool extraRecoveryTurn
    )
    {
        MaterialBoundShaZhaoState[material] =
            string.Empty;

        // 送回蛊恢复堆（飞行动画），并从当前回合开始从零强制恢复：
        // 无视封存前已有的恢复进度，完整恢复周期重新计算；
        // extraRecoveryTurn（主动解体 / 消耗牌杀招）再额外延后 1 回合。
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
            extraRecoveryTurn ? 1 : 0
        );
    }

    /// <summary>
    /// 打出“杀招推演”系统牌的推演入口。
    ///
    /// 成功返回 true（推演牌应消耗）；取消、配方无效或资源不足
    /// 返回 false（推演牌回到手牌，不产生任何惩罚）。
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

        if (playerCombatState.Energy < ActivationEnergyCost ||
            !selectedCards.All(card =>
                ReferenceEquals(card.Owner, player) &&
                card.Pile?.Type == GuCardPileSystem.PileType &&
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

        int yuanQiCost = CalculateShaZhaoYuanQiCost(
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
            RemoveUncommittedResult(shaZhao);
            ShowSynthesisFailure(player, "insufficientResources");
            return false;
        }

        // 支付推演牌自身 1 点能量。
        await PlayerCmd.LoseEnergy(
            ActivationEnergyCost,
            player
        );

        // 材料封装：材料移入隐藏材料区并绑定到杀招。
        await shaZhao.BindMaterialsAsync(selectedCards);

        // 登记每场推演次数；八至九转补发第二张推演牌。
        await ApertureSystem.RegisterShaZhaoDerivationAsync(player);

        // 杀招加入手牌（满手时入弃牌堆）。
        bool addedToHand =
            await GuCardPileSystem.AddGeneratedCardToHand(
                shaZhao,
                player
            );
        if (!addedToHand)
        {
            await CardPileCmd.AddGeneratedCardToCombat(
                shaZhao,
                PileType.Discard,
                player
            );
        }

        Entry.Logger.Info(
            $"[杀招推演] 成功推演 {shaZhao.GetType().Name}，消耗元气 {yuanQiCost}，材料 {selectedCards.Count} 张。"
        );

        return true;
    }
}
