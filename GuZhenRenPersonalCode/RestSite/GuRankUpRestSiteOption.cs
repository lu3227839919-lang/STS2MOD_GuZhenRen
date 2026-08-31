using Godot;

using GuZhenRen.Cards;
using GuZhenRen.Patches;

using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;

using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.RestSite;

/// <summary>
/// 篝火选项：每次升炼共有 2 个槽位，凡蛊各占 1 个、仙蛊各占 2 个，
/// 最多可升炼 2 只凡蛊或 1 只仙蛊（凡仙不混合，因仙蛊已占满 2 槽）。
/// 每张牌都通过原生升级前后预览单独确认；五转升六转也由专用预览
/// 范围支持。取消后续选择会保留本次已经确认的升炼结果。
/// 每个休息点只能成功使用一次。
/// </summary>
public sealed class GuRankUpRestSiteOption
    : ModRestSiteOptionTemplate
{
    internal const string OptionIdentifier =
        "GU_ZHEN_REN_PERSONAL_GU_RANK_UP";

    private static readonly LocString DescriptionText =
        new(
            "rest_site_ui",
            "OPTION_GU_ZHEN_REN_PERSONAL_GU_RANK_UP.description"
        );

    private static readonly LocString SelectionPrompt =
        new(
            "rest_site_ui",
            "OPTION_GU_ZHEN_REN_PERSONAL_GU_RANK_UP.selectionPrompt"
        );

    private const string FallbackIconPath =
        "res://images/ui/rest_site/option_smith.png";

    private static readonly string ShengLianIconPath =
        $"{Entry.ResPath}/images/rest_site_options/GuShengLian.png";

    private readonly List<CardModel> _lastLocalVfxCards = [];

    public GuRankUpRestSiteOption(Player owner)
        : base(owner)
    {
    }

    public override string OptionId =>
        OptionIdentifier;

    public override LocString Description =>
        DescriptionText;

    public override bool IsEnabled =>
        HasEligibleCard(Owner);

    /// <summary>
    /// 升炼篝火按钮图标。
    /// 将 PNG 文件放在：
    /// GuZhenRenPersonal/images/rest_site_options/GuShengLian.png
    ///
    /// 图标尚未放入工程时使用原版锻造图标，确保预加载与按钮渲染正常。
    /// </summary>
    public override RestSiteOptionAssetProfile AssetProfile =>
        new(
            IconPath: Godot.ResourceLoader.Exists(
                ShengLianIconPath
            )
                ? ShengLianIconPath
                : FallbackIconPath
        );

    public override IEnumerable<string> AssetPaths =>
        base.AssetPaths.Concat(
            NCardSmithVfx.AssetPaths
        );

    public override async Task<bool> OnSelect()
    {
        _lastLocalVfxCards.Clear();

        Entry.Logger.Info(
            "点击篝火升炼：" +
            $"玩家 {Owner.NetId}，" +
            $"可用={IsEnabled}。"
        );

        if (!IsEnabled)
        {
            return false;
        }

        bool increased = await TryPerformRankUpOnce();

        if (!increased)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 执行单次升炼。返回 false 表示没有可升炼的蛊卡，
    /// 或玩家取消了本次选择。
    /// </summary>
    private async Task<bool> TryPerformRankUpOnce()
    {
        if (!HasEligibleCard(Owner))
        {
            return false;
        }

        // 使用原生升级选择界面逐张确认，玩家会看到明确的升转前后
        // 预览。凡蛊消耗 1 槽，仙蛊消耗 2 槽；已升炼的牌不会在
        // 本次休息点的下一轮选择中再次出现。
        int remainingSlots = 2;
        HashSet<CardModel> handledCards = [];

        while (remainingSlots > 0)
        {
            bool hasCandidate = Owner.Deck.Cards.Any(card =>
                !handledCards.Contains(card) &&
                IsEligibleCard(card) &&
                GetSlotCost(card) <= remainingSlots
            );

            if (!hasCandidate)
            {
                break;
            }

            CardSelectorPrefs prefs = new(
                SelectionPrompt,
                1
            )
            {
                Cancelable = true,
                // 即使只剩一张候选，也必须打开前后预览供玩家确认。
                RequireManualConfirmation = true,
            };

            CardModel? selected;
            using (
                GuRankUpPreviewPatch.Begin(
                    remainingSlots,
                    handledCards
                )
            )
            {
                selected = (
                    await CardSelectCmd.FromDeckForUpgrade(
                        Owner,
                        prefs
                    )
                ).FirstOrDefault();
            }

            if (selected is not AbstractGuZhenRenCard selectedGu ||
                selected is not IGuWormCard)
            {
                break;
            }

            handledCards.Add(selectedGu);
            int slotCost = GetSlotCost(selectedGu);
            int previousRank = selectedGu.GuRank;

            if (slotCost > remainingSlots ||
                !selectedGu.TryIncreaseGuRank())
            {
                Entry.Logger.Info(
                    "篝火升炼跳过未能提升的蛊牌：" +
                    $"{selectedGu.Id}，当前 {selectedGu.GuRank} 转。"
                );
                continue;
            }

            remainingSlots -= slotCost;
            _lastLocalVfxCards.Add(selectedGu);

            Entry.Logger.Info(
                "篝火升炼完成：" +
                $"{selectedGu.Id} 从 {previousRank} 转提升至 " +
                $"{selectedGu.GuRank} 转。"
            );
        }

        if (_lastLocalVfxCards.Count == 0)
        {
            Entry.Logger.Info(
                "本次篝火升炼未选择蛊牌，已取消。"
            );
            return false;
        }

        Entry.Logger.Info(
            $"本次篝火共成功升炼 {_lastLocalVfxCards.Count} 张蛊牌。"
        );

        return _lastLocalVfxCards.Count > 0;
    }

    public override async Task DoLocalPostSelectVfx(
        CancellationToken ct = default
    )
    {
        if (_lastLocalVfxCards.Count == 0)
        {
            return;
        }

        NCardSmithVfx? vfx =
            NCardSmithVfx.Create(
                _lastLocalVfxCards
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

    private static bool HasEligibleCard(Player player)
    {
        return player.Deck.Cards.Any(IsEligibleCard);
    }

    private static bool IsEligibleCard(CardModel card)
    {
        return card is AbstractGuZhenRenCard gu &&
            card is IGuWormCard &&
            gu.GuRank < gu.MaxGuRank &&
            GuZhenRenCardRules.CanReachGuRank(
                gu,
                gu.GuRank + 1
            );
    }

    private static int GetSlotCost(CardModel card) =>
        card is IGuRankProvider rankProvider &&
        rankProvider.GuRank < GuZhenRenCardRules.XianGuRank
            ? 1
            : 2;
}
