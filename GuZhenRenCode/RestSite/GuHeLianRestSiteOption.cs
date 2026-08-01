using Godot;

using GuZhenRen.Cards;
using GuZhenRen.Cards.Basic;
using GuZhenRen.Cards.HeLian;

using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.RestSite;

/// <summary>
/// 篝火选项：玩家选择一张或多张蛊虫牌并手动确认；
/// 材料数量由实际可制作配方决定。每个休息点只能成功合练一次。
/// </summary>
public sealed class GuHeLianRestSiteOption
    : ModRestSiteOptionTemplate
{
    internal const string OptionIdentifier =
        "GU_ZHEN_REN_HE_LIAN";

    private const int MinimumSelectableMaterialCount = 1;

    private static readonly LocString DescriptionText =
        new(
            "rest_site_ui",
            "OPTION_GU_ZHEN_REN_HE_LIAN.description"
        );

    private static readonly LocString SelectionPrompt =
        new(
            "rest_site_ui",
            "OPTION_GU_ZHEN_REN_HE_LIAN.selectionPrompt"
        );

    private const string FallbackIconPath =
        "res://images/ui/rest_site/option_smith.png";

    private static readonly string HeLianIconPath =
        $"{Entry.ResPath}/images/rest_site_options/GuHeLian.png";

    private CardModel? _lastLocalVfxCard;

    public GuHeLianRestSiteOption(Player owner)
        : base(owner)
    {
    }

    public override string OptionId =>
        OptionIdentifier;

    public override LocString Description =>
        DescriptionText;

    public override bool IsEnabled =>
        HasCraftableRecipe(Owner);

    /// <summary>
    /// 合练篝火按钮图标。
    /// 将 PNG 文件放在：
    /// GuZhenRen/images/rest_site_options/GuHeLian.png
    ///
    /// 图标尚未放入工程时使用原版锻造图标，确保预加载与按钮渲染正常。
    /// </summary>
    public override RestSiteOptionAssetProfile AssetProfile =>
        new(
            IconPath: Godot.ResourceLoader.Exists(
                HeLianIconPath
            )
                ? HeLianIconPath
                : FallbackIconPath
        );

    public override IEnumerable<string> AssetPaths =>
        base.AssetPaths.Concat(
            NCardSmithVfx.AssetPaths
        );

    public override async Task<bool> OnSelect()
    {
        _lastLocalVfxCard = null;

        Entry.Logger.Info(
            "点击篝火合练：" +
            $"玩家 {Owner.NetId}，" +
            $"可用={IsEnabled}。"
        );

        if (!IsEnabled)
        {
            return false;
        }

        bool completed = await TryPerformHeLianOnce();

        if (!completed)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 执行单次合练。返回 false 表示当前没有可用配方，
    /// 玩家取消了材料选择，或所选组合不匹配任何配方。
    /// </summary>
    private async Task<bool> TryPerformHeLianOnce()
    {
        CardModel[] availableMaterials =
            GetAvailableMaterials(Owner);

        if (!HeLianRecipeRegistry
            .TryGetCraftableMaterialCountRange(
                availableMaterials,
                out _,
                out int maximumMaterialCount
            ))
        {
            ShowHeLianFeedback(
                success: false,
                zhsBody:
                    "当前牌组中没有能够完成的合练配方。",
                engBody:
                    "There is no craftable fusion recipe in the current deck."
            );
            return false;
        }

        CardSelectorPrefs prefs = new(
            SelectionPrompt,
            MinimumSelectableMaterialCount,
            maximumMaterialCount
        )
        {
            Cancelable = true,
            RequireManualConfirmation = true,
        };

        List<CardModel> selectedCards =
            (
                await CardSelectCmd.FromDeckGeneric(
                    player: Owner,
                    prefs: prefs,
                    filter: IsEligibleMaterial,
                    sortingOrder: GetMaterialSortOrder
                )
            )
            .ToList();

        if (selectedCards.Count == 0)
        {
            return false;
        }

        if (!HeLianRecipeRegistry.TryCreateResult(
                selectedCards,
                Owner,
                out AbstractGuZhenRenCard? result
            ))
        {
            string selectedMaterialNames =
                string.Join(
                    " + ",
                    selectedCards.Select(card =>
                        card.Title
                    )
                );

            Entry.Logger.Info(
                "合练失败：所选材料未匹配任何合练配方。" +
                $" 材料={selectedMaterialNames}"
            );

            ShowHeLianFeedback(
                success: false,
                zhsBody:
                    $"所选材料无法合练：\n{selectedMaterialNames}\n\n" +
                    "请检查材料种类和数量。",
                engBody:
                    $"The selected materials do not match a fusion recipe:\n" +
                    $"{selectedMaterialNames}\n\n" +
                    "Check the material types and quantities."
            );
            return false;
        }

        List<MaterialSnapshot> materialSnapshots =
            selectedCards
                .Select(card =>
                    new MaterialSnapshot(
                        card,
                        Owner.Deck.Cards
                            .ToList()
                            .IndexOf(card)
                    )
                )
                .OrderBy(snapshot => snapshot.DeckIndex)
                .ToList();

        var playerHistory =
            Owner.RunState
                .CurrentMapPointHistoryEntry?
                .GetEntry(Owner.NetId);

        int removedHistoryCount =
            playerHistory?.CardsRemoved.Count ?? 0;
        int gainedHistoryCount =
            playerHistory?.CardsGained.Count ?? 0;

        try
        {
            // 合练不受同名牌数量与仙蛊唯一规则限制。
            // 所有联机端通过相同选择结果确定性执行相同牌组命令。
            using (GuHeLianScope.Enter())
            {
                await CardPileCmd.RemoveFromDeck(
                    selectedCards,
                    false
                );

                CardPileAddResult addResult =
                    await CardPileCmd.Add(
                        result,
                        PileType.Deck
                    );

                if (!addResult.success)
                {
                    throw new InvalidOperationException(
                        $"合练结果牌 {result.Id} 加入牌组失败。"
                    );
                }
            }

            string materialNames =
                string.Join(
                    " + ",
                    selectedCards.Select(card =>
                        card.Title
                    )
                );

            _lastLocalVfxCard = result;

            Entry.Logger.Info(
                "合练完成：" +
                $"{string.Join(" + ", selectedCards.Select(card => card.Id))} " +
                $"-> {result.Id}，结果为 {result.GuRank} 转。"
            );

            ShowHeLianFeedback(
                success: true,
                zhsBody:
                    $"{materialNames}\n→ [gold]{result.Title}[/gold]\n\n" +
                    $"结果转数：{result.GuRank} 转",
                engBody:
                    $"{materialNames}\n→ [gold]{result.Title}[/gold]\n\n" +
                    $"Result rank: {result.GuRank}"
            );

            return true;
        }
        catch (Exception operationException)
        {
            try
            {
                RollBackFailedHeLian(
                    result,
                    materialSnapshots,
                    playerHistory,
                    removedHistoryCount,
                    gainedHistoryCount
                );
            }
            catch (Exception rollbackException)
            {
                Entry.Logger.Info(
                    "合练失败后的牌组回滚也发生异常：" +
                    rollbackException
                );
            }

            Entry.Logger.Info(
                "合练执行失败，已尝试恢复材料与历史记录：" +
                operationException
            );

            ShowHeLianFeedback(
                success: false,
                zhsBody:
                    "合练执行时发生异常，材料与牌组记录已尝试恢复。\n\n" +
                    operationException.Message,
                engBody:
                    "Fusion failed during execution. The materials and deck " +
                    "history were restored where possible.\n\n" +
                    operationException.Message
            );

            throw;
        }
    }


    /// <summary>
    /// 合练异常时恢复已从运行状态移除的原卡实例，并撤销本次操作
    /// 追加的牌组历史。回滚使用内部牌堆操作，避免再次触发获得/移除钩子。
    /// </summary>
    private static void RollBackFailedHeLian(
        AbstractGuZhenRenCard result,
        IReadOnlyList<MaterialSnapshot> materialSnapshots,
        MegaCrit.Sts2.Core.Runs.PlayerMapPointHistoryEntry? playerHistory,
        int removedHistoryCount,
        int gainedHistoryCount
    )
    {
        using (GuHeLianScope.Enter())
        {
            if (!result.HasBeenRemovedFromState)
            {
                result.RemoveFromState();
            }

            foreach (MaterialSnapshot snapshot in materialSnapshots)
            {
                CardModel card = snapshot.Card;

                if (!card.HasBeenRemovedFromState)
                {
                    continue;
                }

                card.Owner.RunState.AddCard(
                    card,
                    card.Owner
                );

                int restoreIndex = Math.Clamp(
                    snapshot.DeckIndex,
                    0,
                    card.Owner.Deck.Cards.Count
                );

                card.Owner.Deck.AddInternal(
                    card,
                    restoreIndex
                );
            }
        }

        if (playerHistory is null)
        {
            return;
        }

        TrimHistory(
            playerHistory.CardsRemoved,
            removedHistoryCount
        );
        TrimHistory(
            playerHistory.CardsGained,
            gainedHistoryCount
        );
    }

    private static void TrimHistory<T>(
        IList<T> history,
        int originalCount
    )
    {
        while (history.Count > originalCount)
        {
            history.RemoveAt(history.Count - 1);
        }
    }

    private readonly record struct MaterialSnapshot(
        CardModel Card,
        int DeckIndex
    );

    private static bool HasCraftableRecipe(Player player)
    {
        return HeLianRecipeRegistry
            .TryGetCraftableMaterialCountRange(
                GetAvailableMaterials(player),
                out _,
                out _
            );
    }

    private static CardModel[] GetAvailableMaterials(
        Player player
    )
    {
        return player
            .Deck
            .Cards
            .Where(IsEligibleMaterial)
            .ToArray();
    }

    private static bool IsEligibleMaterial(CardModel card)
    {
        // 只允许显式标记为 IGuWormCard 的真正蛊虫卡。
        if (card is not IGuWormCard)
        {
            return false;
        }

        // 杀招推演是固定系统牌，不能被合练移除。
        if (card is ShaZhaoTuiYan)
        {
            return false;
        }

        // 杀招、虚影与专属合练结果不能再次作为源材料。
        if (card is AbstractShaZhaoCard ||
            card is AbstractXuYingCard ||
            card is AbstractHeLianGuCard)
        {
            return false;
        }

        return card.Pile?.Type == PileType.Deck &&
            HeLianRecipeRegistry.IsRecipeMaterialType(
                card.GetType()
            );
    }

    /// <summary>
    /// 向本地玩家显示合练成功或失败结果。
    /// 该提示不参与游戏状态与多人同步。
    /// </summary>
    public override async Task DoLocalPostSelectVfx(
        CancellationToken ct = default
    )
    {
        if (_lastLocalVfxCard == null)
        {
            return;
        }

        NCardSmithVfx? vfx =
            NCardSmithVfx.Create(
                new[] { _lastLocalVfxCard }
            );

        if (vfx == null)
        {
            return;
        }

        NRun.Instance?.GlobalUi
            .CardPreviewContainer
            .AddChildSafely(vfx);

        await Cmd.CustomScaledWait(
            1f,
            2f,
            ignoreCombatEnd: false,
            ct
        );
    }

    public override Task DoRemotePostSelectVfx()
    {
        NRestSiteCharacter? characterNode =
            NRestSiteRoom.Instance?
                .Characters
                .FirstOrDefault(character =>
                    character.Player == Owner
                );

        NCardSmithVfx? vfx =
            NCardSmithVfx.Create();

        if (characterNode == null ||
            vfx == null)
        {
            return Task.CompletedTask;
        }

        characterNode.AddChildSafely(vfx);
        vfx.Position = Vector2.Zero;
        return Task.CompletedTask;
    }

    private void ShowHeLianFeedback(
        bool success,
        string zhsBody,
        string engBody
    )
    {
        if (!LocalContext.IsMe(Owner))
        {
            return;
        }

        NModalContainer? modalContainer =
            NModalContainer.Instance;

        if (modalContainer == null ||
            modalContainer.OpenModal != null)
        {
            Entry.Logger.Info(
                "无法显示合练结果弹窗：当前没有可用模态容器，" +
                "或已有其他弹窗打开。"
            );
            return;
        }

        bool isChinese = string.Equals(
            LocManager.Instance.Language,
            "zhs",
            StringComparison.OrdinalIgnoreCase
        );

        string title = isChinese
            ? success
                ? "合练成功"
                : "合练失败"
            : success
                ? "Fusion Successful"
                : "Fusion Failed";

        string body = isChinese
            ? zhsBody
            : engBody;

        NErrorPopup? popup =
            NErrorPopup.Create(
                title,
                body,
                showReportBugButton: false
            );

        if (popup != null)
        {
            modalContainer.Add(popup);
        }
    }

    private static int GetMaterialSortOrder(CardModel card)
    {
        return card is IGuRankProvider provider
            ? provider.GuRank
            : int.MaxValue;
    }
}
