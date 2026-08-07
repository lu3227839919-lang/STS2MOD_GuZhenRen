using System.Runtime.CompilerServices;

using GuZhenRen.Aperture;
using GuZhenRen.Multiplayer;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Utils;

namespace GuZhenRen.Cards.ImmortalEssence;

/// <summary>
/// 把手牌中的仙元牌作为催动仙蛊的可分次消耗货币。
///
/// 最小单位为一次六转仙蛊催动：
/// 青提、红枣、白荔、黄杏分别值 2、4、8、16 个单位；
/// 六至九转仙蛊每次分别消耗 1、2、4、8 个单位。
/// </summary>
public static class ImmortalEssenceSystem
{
    private sealed class PendingExhaustState
    {
        public HashSet<AbstractXianYuanCard> Cards { get; } =
            new(ReferenceEqualityComparer.Instance);
    }

    // 仙元在 Gu 的 BeforeCardPlayed 中归零时不能立刻 Exhaust：
    // 0.110.0 会在当前蛊牌仍位于出牌区时嵌套修改手牌，导致出牌悬挂。
    // 先只登记归零实例，等整次 CardPlay 系列结束后统一移除。
    private static readonly ConditionalWeakTable<
        Player,
        PendingExhaustState
    > PendingExhausts = new();

    private static readonly SavedAttachedState<CardModel, int>
        RemainingUnitsState = new(
            "gu_zhen_ren.immortal_essence_remaining_units",
            () => -1
        );

    public static int GetActivationCost(int guRank)
    {
        if (guRank < ApertureProgression.ImmortalRank)
        {
            return 0;
        }

        int exponent = Math.Clamp(
            guRank - ApertureProgression.ImmortalRank,
            0,
            3
        );
        return 1 << exponent;
    }

    public static int GetRemainingUnits(AbstractXianYuanCard card)
    {
        ArgumentNullException.ThrowIfNull(card);

        int saved = RemainingUnitsState[card];
        return saved < 0
            ? card.ActivationUnits
            : Math.Clamp(saved, 0, card.ActivationUnits);
    }

    public static int GetAvailableUnits(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);

        return player.PlayerCombatState?.Hand.Cards
            .OfType<AbstractXianYuanCard>()
            .Sum(GetRemainingUnits) ?? 0;
    }

    public static bool CanPayForActivation(CardModel card)
    {
        ArgumentNullException.ThrowIfNull(card);

        if (card is not IGuWormCard guCard ||
            guCard.GuRank < ApertureProgression.ImmortalRank)
        {
            return true;
        }

        Player? owner = card.Owner;
        return owner?.PlayerCombatState != null &&
               GetAvailableUnits(owner) >=
               GetActivationCost(guCard.GuRank);
    }

    /// <summary>
    /// 在仙蛊出牌序列的第一段结算仙元。归零的仙元牌先登记为
    /// 待消耗，等本次出牌系列结束后再进入消耗牌堆。
    /// </summary>
    public static async Task<bool> SpendForActivation(
        CardPlay cardPlay
    )
    {
        ArgumentNullException.ThrowIfNull(cardPlay);

        if (!cardPlay.IsFirstInSeries ||
            cardPlay.Card is not IGuWormCard guCard ||
            guCard.GuRank < ApertureProgression.ImmortalRank)
        {
            return true;
        }

        Player player = cardPlay.Player;
        int remainingCost = GetActivationCost(guCard.GuRank);

        AbstractXianYuanCard[] availableCards =
            player.PlayerCombatState?.Hand.Cards
                .OfType<AbstractXianYuanCard>()
                .OrderBy(GetRemainingUnits)
                .ThenBy(card => card.Id.Entry, StringComparer.Ordinal)
                .ThenBy(GuZhenRenDeterminism.GetCardNetworkId)
                .ToArray() ?? [];

        if (availableCards.Sum(GetRemainingUnits) < remainingCost)
        {
            return false;
        }

        List<AbstractXianYuanCard> depletedCards = [];

        foreach (AbstractXianYuanCard essence in availableCards)
        {
            if (remainingCost <= 0)
            {
                break;
            }

            int current = GetRemainingUnits(essence);
            int spent = Math.Min(current, remainingCost);
            int newAmount = current - spent;

            RemainingUnitsState[essence] = newAmount;
            remainingCost -= spent;

            if (newAmount == 0)
            {
                depletedCards.Add(essence);
            }
        }

        if (depletedCards.Count > 0)
        {
            PendingExhaustState state =
                PendingExhausts.GetValue(
                    player,
                    static _ => new PendingExhaustState()
                );

            foreach (AbstractXianYuanCard depleted in depletedCards)
            {
                state.Cards.Add(depleted);
            }
        }

        await Task.CompletedTask;
        return remainingCost == 0;
    }

    /// <summary>
    /// 在蛊牌的最后一段 CardPlay 完成后，统一消耗余额为零的仙元牌。
    /// 这样既保留“耗尽时消耗”的规则，也避免在 BeforeCardPlayed
    /// 中嵌套修改手牌造成当前蛊牌悬挂。
    /// </summary>
    internal static async Task ExhaustDepletedCardsAsync(
        Player player
    )
    {
        ArgumentNullException.ThrowIfNull(player);

        if (!PendingExhausts.TryGetValue(
                player,
                out PendingExhaustState? state
            ))
        {
            return;
        }

        AbstractXianYuanCard[] pending =
            state.Cards
                .OrderBy(GuZhenRenDeterminism.GetCardNetworkId)
                .ToArray();

        foreach (AbstractXianYuanCard depleted in pending)
        {
            state.Cards.Remove(depleted);

            if (depleted.Pile?.Type != PileType.Hand ||
                GetRemainingUnits(depleted) > 0)
            {
                continue;
            }

            if (depleted.CombatState == null)
            {
                // 兼容由旧版本创建、未登记进 CombatState 的战斗实例。
                // 不再调用必然抛错的 CardCmd.Exhaust；本场结束后该临时牌
                // 会随战斗状态清理。新生成的仙元已经统一由 CombatState
                // 创建，因此正常流程不会进入此分支。
                Entry.Logger.Warn(
                    $"[仙元] 跳过未登记战斗状态的耗尽牌 {depleted.Id}；" +
                    "请在下一场战斗使用重新生成的仙元实例。"
                );
                continue;
            }

            await CardExhaustCompat.ExhaustAsync(
                new BlockingPlayerChoiceContext(),
                depleted
            );
        }

        if (state.Cards.Count == 0)
        {
            PendingExhausts.Remove(player);
        }
    }
}
