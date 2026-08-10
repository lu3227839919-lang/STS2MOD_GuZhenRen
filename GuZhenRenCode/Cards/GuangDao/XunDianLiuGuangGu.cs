using GuZhenRen.Characters;
using GuZhenRen.Combat;
using GuZhenRen.Multiplayer;
using GuZhenRen.Powers.GuangDao;

using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.GuangDao;

/// <summary>
/// 迅电流光蛊：平时加速普通手牌过渡；耀化后改为在存放堆与
/// 已完全恢复的待命蛊之间进行等量切换。
/// </summary>
[RegisterCard(typeof(GuZhenRenGuCardPool))]
public sealed class XunDianLiuGuangGu : AbstractGuWormCard
{
    private const int GuangHuiCost = 2;
    private const string DrawVar = "Draw";
    private const string DiscardVar = "Discard";
    private const string SwapVar = "Swap";

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        base.CanonicalKeywords
            .Append(GuZhenRenKeywords.GetYaoHuaKeyword(2))
            .Distinct();

    public override int MaxUses => 1;

    public override int RecoveryDelayTurns => GuRank switch
    {
        <= 5 => 2,
        <= 8 => 3,
        _ => 4,
    };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar(DrawVar, 2m),
        new DynamicVar(DiscardVar, 1m),
        new DynamicVar(SwapVar, 1m),
    ];

    // 暂用现有流光蛊卡图，避免缺图时出现空白卡面。
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: CardImageCatalog.GetResourcePath(typeof(LiuGuangGu))
    );

    public XunDianLiuGuangGu()
        : base(0, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
        SetDao(Dao.GuangDao);
        this.SecondaryCosts().Set(YuanQiSystem.ResourceId, 1);
        RefreshRankValues();
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        CardModel[] activeCandidates = GetActiveSwapCandidates();
        CardModel[] standbyCandidates = GetStandbySwapCandidates();
        int swapLimit = Math.Min(
            DynamicVars[SwapVar].IntValue,
            Math.Min(activeCandidates.Length, standbyCandidates.Length)
        );

        bool canSwap = swapLimit > 0;
        bool empowered = canSwap &&
            await GuangDaoPowerSystem.TryAutoSpendGuangHui(
                choiceContext,
                this,
                cardPlay,
                GuangHuiCost
            );

        if (!empowered)
        {
            await DrawAndDiscard(choiceContext);
            return;
        }

        await SwapGuCards(
            choiceContext,
            activeCandidates,
            standbyCandidates,
            swapLimit
        );
    }

    private async Task DrawAndDiscard(
        PlayerChoiceContext choiceContext
    )
    {
        await CardPileCmd.Draw(
            choiceContext,
            DynamicVars[DrawVar].BaseValue,
            Owner
        );

        int discardCount = Math.Min(
            DynamicVars[DiscardVar].IntValue,
            PileType.Hand.GetPile(Owner).Cards.Count
        );
        if (discardCount <= 0)
        {
            return;
        }

        IEnumerable<CardModel> selected =
            await CardSelectCmd.FromHandForDiscard(
                choiceContext,
                Owner,
                new CardSelectorPrefs(
                    CardSelectorPrefs.DiscardSelectionPrompt,
                    discardCount
                )
                {
                    Cancelable = false,
                },
                filter: null,
                source: this
            );

        await CardCmd.Discard(choiceContext, selected);
    }

    private async Task SwapGuCards(
        PlayerChoiceContext choiceContext,
        IReadOnlyList<CardModel> activeCandidates,
        IReadOnlyList<CardModel> standbyCandidates,
        int swapLimit
    )
    {
        CardSelectorPrefs outboundPrefs = new(
            SelectionScreenPrompt,
            minCount: 1,
            maxCount: swapLimit
        )
        {
            Cancelable = false,
            PretendCardsCanBePlayed = true,
        };

        CardModel[] outbound = (
            await CardSelectCmd.FromSimpleGrid(
                choiceContext,
                activeCandidates,
                Owner,
                outboundPrefs
            )
        )
            .OrderBy(GuZhenRenDeterminism.GetCardNetworkId)
            .ToArray();

        if (outbound.Length == 0)
        {
            return;
        }

        LocString inboundPrompt = new(
            "cards",
            "GU_ZHEN_REN_CARD_XUN_DIAN_LIU_GUANG_GU.recoverySelectionPrompt"
        );
        CardModel[] inbound = (
            await CardSelectCmd.FromSimpleGrid(
                choiceContext,
                standbyCandidates,
                Owner,
                new CardSelectorPrefs(inboundPrompt, outbound.Length)
                {
                    Cancelable = false,
                    PretendCardsCanBePlayed = true,
                }
            )
        )
            .OrderBy(GuZhenRenDeterminism.GetCardNetworkId)
            .ToArray();

        // 两边都选完才开始移动，避免选择流程被打断时出现半次交换。
        if (inbound.Length != outbound.Length)
        {
            Entry.Logger.Warn(
                "[迅电流光蛊] 待命蛊选择数量异常，本次交换未执行。"
            );
            return;
        }

        foreach (CardModel card in outbound)
        {
            await GuCardPileSystem.MoveCardToPileAsync(
                card,
                GuCardPileSystem.RecoveryPileType
            );
        }

        foreach (CardModel card in inbound)
        {
            await GuCardPileSystem.MoveCardToPileAsync(
                card,
                GuCardPileSystem.PileType
            );
        }
    }

    private CardModel[] GetActiveSwapCandidates() =>
        GuCardPileSystem.PileType
            .GetPile(Owner)
            .Cards
            .Where(card =>
                !ReferenceEquals(card, this) &&
                card is IGuWormCard &&
                GuCardUsageRules.CanUse(card) &&
                !GuCardUsageRules.HasRecoverySchedule(card) &&
                !ShaZhaoTuiYanSystem.IsMaterialSealed(card)
            )
            .OrderBy(GuZhenRenDeterminism.GetCardNetworkId)
            .ToArray();

    private CardModel[] GetStandbySwapCandidates() =>
        GuCardPileSystem.RecoveryPileType
            .GetPile(Owner)
            .Cards
            .Where(card =>
                card is IGuWormCard &&
                GuCardUsageRules.CanUse(card) &&
                !GuCardUsageRules.HasRecoverySchedule(card) &&
                !ShaZhaoTuiYanSystem.IsMaterialSealed(card)
            )
            .OrderBy(GuZhenRenDeterminism.GetCardNetworkId)
            .ToArray();

    protected override void OnGuRankChanged()
    {
        base.OnGuRankChanged();
        RefreshRankValues();
    }

    private void RefreshRankValues()
    {
        DynamicVars[DrawVar].BaseValue = GuRank switch
        {
            <= 2 => 2,
            <= 5 => 3,
            <= 7 => 4,
            _ => 5,
        };
        DynamicVars[DiscardVar].BaseValue = GuRank switch
        {
            <= 2 => 1,
            <= 5 => 2,
            _ => 3,
        };
        DynamicVars[SwapVar].BaseValue = GuRank switch
        {
            <= 3 => 1,
            <= 7 => 2,
            _ => 3,
        };
    }

    protected override void OnUpgrade()
    {
    }
}
