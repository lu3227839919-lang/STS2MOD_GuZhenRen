using System.Reflection;

using GuZhenRen.Aperture;
using GuZhenRen.Cards.ImmortalEssence;
using GuZhenRen.Cards.ShaZhao;
using GuZhenRen.Characters;
using GuZhenRen.Combat;
using GuZhenRen.Multiplayer;
using GuZhenRen.Tribulations.Core;

using HarmonyLib;

using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interactions.RightClick;

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
                    typeof(ShaZhaoBindingService),
                    nameof(ShaZhaoBindingService.RemoveFromCombatPostfix)
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
                    typeof(ShaZhaoBindingService),
                    nameof(ShaZhaoBindingService.AfterCombatEndPrefix)
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

    private static async Task RemoveUncommittedResultAsync(
        AbstractShaZhaoCard shaZhao
    )
    {
        if (shaZhao.Pile?.IsCombatPile == true)
        {
            await CardPileCmd.RemoveFromCombat(shaZhao, skipVisuals: true);
            return;
        }

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
    /// 选择界面暂停同步行动后，按 NetCombatCard 编号在每端重新解析
    /// 材料，确保重复同名蛊也指向同一战斗实例。
    /// </summary>
    private static bool TryResolveSelectedMaterials(
        Player player,
        IReadOnlyList<CardModel> selected,
        out List<CardModel> resolved
    )
    {
        resolved = [];
        uint[] networkIds = selected
            .Select(GuZhenRenDeterminism.GetCardNetworkId)
            .ToArray();
        if (networkIds.Any(static id => id == uint.MaxValue) ||
            networkIds.Distinct().Count() != networkIds.Length)
        {
            return false;
        }

        Dictionary<uint, CardModel> availableById =
            GuCardPileSystem.PileType.GetPile(player).Cards
                .Concat(GuCardPileSystem.StoragePileType.GetPile(player).Cards)
                .Distinct()
                .Select(card => new
                {
                    Card = card,
                    NetworkId = GuZhenRenDeterminism.GetCardNetworkId(card),
                })
                .Where(static item => item.NetworkId != uint.MaxValue)
                .GroupBy(static item => item.NetworkId)
                .Where(static group => group.Count() == 1)
                .ToDictionary(
                    static group => group.Key,
                    static group => group.Single().Card
                );

        foreach (uint networkId in networkIds)
        {
            if (!availableById.TryGetValue(networkId, out CardModel? material) ||
                !ReferenceEquals(material.Owner, player) ||
                !IsEligibleMaterial(material))
            {
                resolved.Clear();
                return false;
            }
            resolved.Add(material);
        }

        return true;
    }

    /// <summary>
    /// 打出“杀招推演”系统牌的推演入口。
    ///
    /// 成功返回 true（推演牌应消耗），且只有成功收口时才实际消耗元气；
    /// 取消、配方无效、资源不足或其他正常失败返回 false，
    /// 推演牌负责回到手牌并回滚原生能量/星星支付。
    /// </summary>
    public static async Task<bool> DeriveFromCardAsync(
        PlayerChoiceContext choiceContext,
        Player player,
        CardPlay derivationPlay
    )
    {
        if (player.PlayerCombatState == null)
            return false;

        CardPile guPile = GuCardPileSystem.PileType.GetPile(player);
        (Type? targetCardType, List<CardModel> selectedCards) =
            await SelectTargetAndMaterialsAsync(choiceContext, player, guPile);
        if (targetCardType == null || selectedCards.Count == 0)
            return false;

        if (!TryResolveSelectedMaterials(player, selectedCards, out selectedCards))
        {
            ShowSynthesisFailure(player, "stateChanged");
            return false;
        }
        if (!ShaZhaoRecipeRegistry.HasMatchingRecipe(selectedCards, targetCardType))
        {
            ShowSynthesisFailure(player, "invalidRecipe");
            return false;
        }

        int materialMaxRank = selectedCards.Max(
            static card => AbstractShaZhaoCard.GetMaterialRank(card)
        );
        bool isImmortal = materialMaxRank >= ApertureProgression.ImmortalRank;
        int xianYuanCost = isImmortal
            ? ImmortalEssenceSystem.GetActivationCost(materialMaxRank)
            : 0;
        int yuanQiCost = isImmortal
            ? 0
            : CalculateShaZhaoYuanQiCost(player, selectedCards);

        if (isImmortal &&
            ImmortalEssenceSystem.GetAvailableUnits(player) < xianYuanCost)
        {
            ShowSynthesisFailure(player, "insufficientXianYuan");
            return false;
        }
        if (!isImmortal &&
            SecondaryResourceCmd.Get(player, YuanQiSystem.ResourceId) < yuanQiCost)
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

        shaZhao.EnergyCost.SetUntilPlayed(0);
        bool addedToHand = await HandCapacityExemptionPatch
            .AddGeneratedShaZhaoToHandAsync(shaZhao, player);
        if (!addedToHand)
        {
            await RemoveUncommittedResultAsync(shaZhao);
            ShowSynthesisFailure(player, "creationFailed");
            return false;
        }

        bool paid = isImmortal
            ? await ImmortalEssenceSystem.SpendUnits(player, xianYuanCost)
            : yuanQiCost == 0 || await SecondaryResourceCmd.Spend(
                player,
                YuanQiSystem.ResourceId,
                yuanQiCost,
                card: shaZhao,
                source: shaZhao
            );
        if (!paid)
        {
            await RemoveUncommittedResultAsync(shaZhao);
            ShowSynthesisFailure(
                player,
                isImmortal ? "insufficientXianYuan" : "insufficientResources"
            );
            return false;
        }

        if (!isImmortal && yuanQiCost > 0)
            await TribulationSystem.EventRouter.OnYuanQiSpentAsync(
                player,
                yuanQiCost
            );

        await shaZhao.BindMaterialsAsync(selectedCards);
        await GuCardPileSystem.RefillGuHandAsync(player);
        if (shaZhao.MaterialsSealedPermanently)
            shaZhao.ClearBoundMaterials();
        await ApertureSystem.RegisterShaZhaoDerivationAsync(player);

        Entry.Logger.Info(
            $"[杀招推演] 成功推演 {shaZhao.GetType().Name}，" +
            $"原生支付能量 {derivationPlay.Resources.EnergySpent}、" +
            $"星星 {derivationPlay.Resources.StarsSpent}，" +
            $"元气 {yuanQiCost}、仙元 {xianYuanCost}，" +
            $"材料 {selectedCards.Count} 张。"
        );
        return true;
    }
}
