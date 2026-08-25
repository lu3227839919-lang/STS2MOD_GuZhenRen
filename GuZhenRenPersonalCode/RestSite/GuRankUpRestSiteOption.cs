using Godot;

using GuZhenRen.Cards;

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
/// 玩家在单次选牌界面直接混合选择，无需先选再追加；
/// 槽位上限由 DeckCardSelectionManualConfirmationPatch 在选牌界面强制。
/// 五转升六转同样由升炼完成。
/// 选择 0 张并确认等同于取消；每个休息点只能成功使用一次。
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

        // 升炼共有 2 个槽位：凡蛊每只占 1 个（最多 2 只）、仙蛊每只占 2 个
        // （最多 1 只）。玩家在单次选牌界面直接混合选择；
        // 槽位上限由 DeckCardSelectionManualConfirmationPatch 在点击时动态强制。
        const int totalSlots = 2;

        CardSelectorPrefs prefs = new(
            SelectionPrompt,
            0,
            totalSlots
        )
        {
            Cancelable = true,
            RequireManualConfirmation = true
        };

        IEnumerable<CardModel> selection =
            await CardSelectCmd.FromDeckGeneric(
                player: Owner,
                prefs: prefs,
                filter: IsEligibleCard,
                sortingOrder: card =>
                    card is AbstractGuZhenRenCard gu
                        ? gu.GuRank
                        : int.MaxValue
            );

        List<AbstractGuZhenRenCard> selectedGuCards =
            selection
                .OfType<AbstractGuZhenRenCard>()
                .Take(totalSlots)
                .ToList();

        if (selectedGuCards.Count == 0)
        {
            Entry.Logger.Info(
                "本次篝火升炼未选择蛊牌，已取消。"
            );
            return false;
        }

        foreach (AbstractGuZhenRenCard selectedGu in
                 selectedGuCards)
        {
            if (!selectedGu.TryIncreaseGuRank())
            {
                Entry.Logger.Info(
                    "篝火升炼跳过未能提升的蛊牌：" +
                    $"{selectedGu.Id}，当前 {selectedGu.GuRank} 转。"
                );
                continue;
            }

            _lastLocalVfxCards.Add(selectedGu);

            Entry.Logger.Info(
                "篝火升炼完成：" +
                $"{selectedGu.Id} 提升至 " +
                $"{selectedGu.GuRank} 转。"
            );
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
}