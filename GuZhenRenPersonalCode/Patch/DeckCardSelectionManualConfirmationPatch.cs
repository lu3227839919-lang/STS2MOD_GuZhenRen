using System.Reflection;

using GuZhenRen.Cards;

using HarmonyLib;

using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;

namespace GuZhenRen.Patches;

/// <summary>
/// 修正牌组选择界面对 RequireManualConfirmation 的处理。
///
/// 原版 NDeckCardSelectScreen 在选中数量达到 MaxSelect 时会直接进入
/// 预览确认页，没有检查 RequireManualConfirmation。对于合练这种允许
/// 选择不同数量材料、并要求玩家主动点击确认的流程，这会导致达到
/// MaxSelect 后立即跳转。
///
/// 本补丁只接管 RequireManualConfirmation=true 且 MinSelect < MaxSelect
/// 的可变数量牌组选择，包括升炼的直接混合选择。
/// 固定数量选择继续使用原版预览确认流程。
/// </summary>
internal static class DeckCardSelectionManualConfirmationPatch
{
    private const string HarmonyId =
        Entry.ModId + ".DeckCardSelectionManualConfirmation";

    private static FieldInfo? _selectedCardsField;
    private static FieldInfo? _prefsField;
    private static FieldInfo? _gridField;
    private static MethodInfo? _refreshConfirmButtonVisibility;
    private static bool _initialized;

    internal static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        MethodInfo? onCardClicked =
            AccessTools.DeclaredMethod(
                typeof(NDeckCardSelectScreen),
                "OnCardClicked",
                [typeof(CardModel)]
            );

        _selectedCardsField = AccessTools.Field(
            typeof(NDeckCardSelectScreen),
            "_selectedCards"
        );
        _prefsField = AccessTools.Field(
            typeof(NDeckCardSelectScreen),
            "_prefs"
        );
        _gridField = AccessTools.Field(
            typeof(NCardGridSelectionScreen),
            "_grid"
        );
        _refreshConfirmButtonVisibility =
            AccessTools.DeclaredMethod(
                typeof(NDeckCardSelectScreen),
                "RefreshConfirmButtonVisibility"
            );

        if (onCardClicked == null ||
            _selectedCardsField == null ||
            _prefsField == null ||
            _gridField == null ||
            _refreshConfirmButtonVisibility == null)
        {
            ResetReflectionState();

            throw new MissingMemberException(
                "手动确认选牌补丁所需的 NDeckCardSelectScreen 成员不存在。"
            );
        }

        Harmony harmony = new(HarmonyId);

        try
        {
            harmony.Patch(
                onCardClicked,
                prefix: new HarmonyMethod(
                    typeof(
                        DeckCardSelectionManualConfirmationPatch
                    ),
                    nameof(OnCardClickedPrefix)
                )
            );

            _initialized = true;
        }
        catch
        {
            harmony.UnpatchAll(HarmonyId);
            ResetReflectionState();
            throw;
        }
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
            _initialized = false;
            ResetReflectionState();
        }
    }

    private static bool OnCardClickedPrefix(
        NDeckCardSelectScreen __instance,
        CardModel card
    )
    {
        FieldInfo selectedCardsField =
            _selectedCardsField ??
            throw new InvalidOperationException(
                "手动确认选牌补丁尚未初始化。"
            );
        FieldInfo prefsField =
            _prefsField ??
            throw new InvalidOperationException(
                "手动确认选牌补丁尚未初始化。"
            );
        FieldInfo gridField =
            _gridField ??
            throw new InvalidOperationException(
                "手动确认选牌补丁尚未初始化。"
            );
        MethodInfo refreshConfirmButtonVisibility =
            _refreshConfirmButtonVisibility ??
            throw new InvalidOperationException(
                "手动确认选牌补丁尚未初始化。"
            );

        CardSelectorPrefs prefs =
            (CardSelectorPrefs)(
                prefsField.GetValue(__instance) ??
                throw new InvalidOperationException(
                    "无法读取牌组选择参数。"
                )
            );

        // 固定数量选择在达到上限后直接进入原版预览确认页是正确行为。
        // 只接管 MinSelect < MaxSelect 的可变数量选择，避免锻造、烹饪等
        // 固定数量流程因确认按钮保持禁用而无法完成。
        if (!prefs.RequireManualConfirmation ||
            prefs.MinSelect == prefs.MaxSelect)
        {
            return true;
        }

        HashSet<CardModel> selectedCards =
            (HashSet<CardModel>)(
                selectedCardsField.GetValue(__instance) ??
                throw new InvalidOperationException(
                    "无法读取已选牌集合。"
                )
            );

        NCardGrid grid =
            (NCardGrid)(
                gridField.GetValue(__instance) ??
                throw new InvalidOperationException(
                    "无法读取牌组选择网格。"
                )
            );

        bool isAlreadySelected =
            selectedCards.Contains(card);

        // 原补丁只禁止自动提交，却没有阻止继续添加，导致选择数量可以
        // 超过 MaxSelect。达到上限后仍允许点击已选牌取消选择。
        if (!isAlreadySelected &&
            selectedCards.Count >= prefs.MaxSelect)
        {
            refreshConfirmButtonVisibility.Invoke(
                __instance,
                null
            );
            return false;
        }

        // 升炼共有 2 个槽位：凡蛊每只占 1 个（最多 2 只）、仙蛊每只占 2 个
        // （最多 1 只）。玩家在单次选择中直接混合选牌，累计槽位超过 2 后拒绝再选。
        if (!isAlreadySelected &&
            IsGuRankUpSelection(prefs))
        {
            int slotsUsed = 0;

            foreach (CardModel selected in selectedCards)
            {
                if (selected is AbstractGuZhenRenCard selectedGu)
                {
                    slotsUsed += IsMortalGu(selectedGu) ? 1 : 2;
                }
            }

            int newCardSlots =
                card is AbstractGuZhenRenCard guCard
                    ? IsMortalGu(guCard) ? 1 : 2
                    : 0;

            if (slotsUsed + newCardSlots > 2)
            {
                refreshConfirmButtonVisibility.Invoke(
                    __instance,
                    null
                );
                return false;
            }
        }

        if (isAlreadySelected)
        {
            selectedCards.Remove(card);
            grid.UnhighlightCard(card);
        }
        else
        {
            selectedCards.Add(card);
            grid.HighlightCard(card);
        }

        // 选择任意数量（0 至 2 槽位）时都保持在选牌界面。
        // 不在达到 MaxSelect 时自动进入预览；由玩家点击确认按钮。
        refreshConfirmButtonVisibility.Invoke(
            __instance,
            null
        );

        return false;
    }

    private static void ResetReflectionState()
    {
        _selectedCardsField = null;
        _prefsField = null;
        _gridField = null;
        _refreshConfirmButtonVisibility = null;
    }

    /// <summary>
    /// 通过提示文本标识升炼的选择界面，避免与其他可变数量选择混淆。
    /// </summary>
    private static bool IsGuRankUpSelection(
        CardSelectorPrefs prefs
    )
    {
        return string.Equals(
                prefs.Prompt.LocTable,
                "rest_site_ui",
                StringComparison.Ordinal
            ) &&
            string.Equals(
                prefs.Prompt.LocEntryKey,
                "OPTION_GU_ZHEN_REN_PERSONAL_GU_RANK_UP" +
                    ".selectionPrompt",
                StringComparison.Ordinal
            );
    }

    private static bool IsMortalGu(AbstractGuZhenRenCard gu)
    {
        return gu.GuRank < GuZhenRenCardRules.XianGuRank;
    }
}
