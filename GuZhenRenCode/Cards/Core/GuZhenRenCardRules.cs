using GuZhenRen.Cards.HeLian;
using GuZhenRen.Characters;

using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

using STS2RitsuLib.Utils;

namespace GuZhenRen.Cards;

public enum CardUniqueScope
{
    None,
    PlayerDeck,
    EntireRun,
}

public readonly record struct PlannedCardAddition(
    Player Player,
    CardModel Card
);

public static class GuZhenRenCardRules
{
    public const int XianGuRank = 6;

    // 蛊虫不进入普通抽牌循环，因此以永久牌组容量承担构筑代价。
    public const int GuWormDeckCapacity = 15;

    private static readonly object XianGuMutationSync = new();

    // 0 表示旧存档或尚未登记；其他值保存“首次成为仙蛊的楼层 + 1”。
    // 旧存档中的既有仙蛊被视作最早产生，绝不会被新仙蛊顶替。
    private static readonly SavedAttachedState<CardModel, int>
        XianGuClaimFloorState = new(
            Entry.ModId + ".xian_gu_claim_floor",
            () => 0
        );

    public static bool IsXianGu(CardModel card)
    {
        ArgumentNullException.ThrowIfNull(card);

        return card is IGuWormCard guWorm &&
            guWorm.GuRank >= XianGuRank;
    }

    /// <summary>
    /// 尝试取得应显示在卡牌上的蛊虫转数。
    ///
    /// 杀招虽然复用了转数接口，但不是蛊虫，因此不显示蛊虫转数关键词。
    /// 结果只依赖卡牌自身已保存并参与多人快照的 GuRank，不读取本地玩家。
    /// </summary>
    public static bool TryGetDisplayGuRank(
        CardModel card,
        out int rank
    )
    {
        ArgumentNullException.ThrowIfNull(card);

        rank = card is IGuWormCard guWorm
            ? guWorm.GuRank
            : 0;

        return rank is >= 1 and <= 9;
    }

    public static CardUniqueScope GetUniqueScope(
        CardModel card
    )
    {
        ArgumentNullException.ThrowIfNull(card);

        // 仙蛊的唯一性由实时转数和仙蛊规则独立决定，
        // 与普通“唯一”展示关键词完全无关。
        if (IsXianGu(card))
        {
            return CardUniqueScope.EntireRun;
        }

        return card.Keywords.Contains(
            GuZhenRenKeywords.Unique
        )
            ? CardUniqueScope.PlayerDeck
            : CardUniqueScope.None;
    }

    public static bool CanOfferToPlayer(
        IRunState runState,
        Player receivingPlayer,
        CardModel candidate
    )
    {
        return CanEnterPermanentDeck(
            runState,
            receivingPlayer,
            candidate,
            ignoredExistingCards: null,
            plannedAdditions: null,
            allowSingleGuReplacementAtCapacity: false
        );
    }

    /// <summary>
    /// 奖励和商店在恰好达到蛊虫容量时仍可展示新蛊虫。
    /// 真正获得时必须先选择一张现有蛊虫作为替换对象。
    /// 唯一性与仙蛊冲突规则仍然照常检查。
    /// </summary>
    public static bool CanOfferWithGuReplacement(
        IRunState runState,
        Player receivingPlayer,
        CardModel candidate
    )
    {
        if (!CanEnterPermanentDeck(
                runState,
                receivingPlayer,
                candidate,
                ignoredExistingCards: null,
                plannedAdditions: null,
                allowSingleGuReplacementAtCapacity: true
            ))
        {
            return false;
        }

        if (candidate is not IGuWormCard)
        {
            return true;
        }

        int guCount = receivingPlayer.Deck.Cards.Count(card =>
            card is IGuWormCard
        );

        if (guCount < GuWormDeckCapacity)
        {
            return true;
        }

        // 容量已满时至少要存在一张合法替换对象，避免奖励或商店
        // 展示一张最终无法获得的蛊虫。
        return receivingPlayer.Deck.Cards.Any(existing =>
            CanReplaceGuWorm(existing, candidate)
        );
    }

    internal static bool CanReplaceGuWorm(
        CardModel existing,
        CardModel candidate
    )
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(candidate);

        if (existing is not IGuWormCard ||
            candidate is not IGuWormCard ||
            existing.Pile?.Type != PileType.Deck ||
            !ReferenceEquals(existing.Owner, candidate.Owner))
        {
            return false;
        }

        return CanEnterPermanentDeck(
            candidate.Owner.RunState,
            candidate.Owner,
            candidate,
            ignoredExistingCards:
                new HashSet<CardModel> { existing },
            plannedAdditions: null,
            allowSingleGuReplacementAtCapacity: false
        );
    }

    /// <summary>
    /// 判断卡牌能否进入指定玩家的普通卡牌奖励。
    ///
    /// 蛊真人的所有卡牌奖励只允许真正实现 IGuWormCard 的蛊虫牌。
    /// 该限制不影响其他角色的奖励，也不改变商店、合练、杀招推演、
    /// 战斗生成或其他直接获得卡牌的流程。
    /// </summary>
    public static bool CanAppearInCardReward(
        Player receivingPlayer,
        CardModel candidate
    )
    {
        ArgumentNullException.ThrowIfNull(receivingPlayer);
        ArgumentNullException.ThrowIfNull(candidate);

        if (candidate is ICardRewardExcluded)
        {
            return false;
        }

        if (receivingPlayer.Character is GuZhenRenCharacter &&
            candidate is not IGuWormCard)
        {
            return false;
        }

        // 专属合练蛊默认不进入奖励；只有具体卡牌显式实现
        // IHeLianCardRewardEligible 时才开放。常规蛊虫牌不受此限制。
        return candidate is not AbstractHeLianGuCard ||
            candidate is IHeLianCardRewardEligible;
    }

    public static bool CanAddToDeck(
        IRunState runState,
        CardModel candidate
    )
    {
        ArgumentNullException.ThrowIfNull(candidate);

        return CanEnterPermanentDeck(
            runState,
            candidate.Owner,
            candidate,
            ignoredExistingCards: null,
            plannedAdditions: null
        );
    }

    public static bool CanUseAsTransformationResult(
        CardModel original,
        CardModel replacement,
        IReadOnlySet<CardModel>? ignoredOriginals = null,
        IReadOnlyList<PlannedCardAddition>? plannedAdditions = null
    )
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(replacement);

        // 战斗内转换不改变永久牌组。
        if (original.Pile?.Type != PileType.Deck)
        {
            return true;
        }

        return CanEnterPermanentDeck(
            original.Owner.RunState,
            original.Owner,
            replacement,
            ignoredOriginals,
            plannedAdditions
        );
    }

    public static bool HasSameXianGu(
        IRunState runState,
        CardModel candidate,
        CardModel? ignoredCard = null
    )
    {
        ArgumentNullException.ThrowIfNull(runState);
        ArgumentNullException.ThrowIfNull(candidate);

        return runState.Players
            .SelectMany(player => player.Deck.Cards)
            .Any(existing =>
                !ReferenceEquals(existing, ignoredCard) &&
                IsSameCard(existing, candidate) &&
                IsXianGu(existing)
            );
    }

    public static bool CanReachGuRank(
        CardModel candidate,
        int targetRank
    )
    {
        ArgumentNullException.ThrowIfNull(candidate);

        if (targetRank < XianGuRank ||
            candidate.Pile?.Type != PileType.Deck)
        {
            return true;
        }

        lock (XianGuMutationSync)
        {
            return CanCandidateOwnXianGuClaim(
                candidate.Owner.RunState,
                candidate,
                candidate
            );
        }
    }

    /// <summary>
    /// 将“检查整局唯一性”和“写入六转”放在同一个临界区，并按
    /// 首次成为仙蛊的楼层、玩家槽位、牌组位置进行确定性仲裁。
    /// 即使各客户端先后处理两个玩家的升转消息，最终赢家也相同。
    /// </summary>
    internal static bool TryCommitGuRankIncrease(
        CardModel candidate,
        int targetRank,
        Action commit
    )
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(commit);

        if (targetRank < XianGuRank ||
            candidate.Pile?.Type != PileType.Deck)
        {
            commit();
            return true;
        }

        lock (XianGuMutationSync)
        {
            IRunState runState = candidate.Owner.RunState;

            if (!CanCandidateOwnXianGuClaim(
                    runState,
                    candidate,
                    candidate
                ))
            {
                return false;
            }

            // 先提交候选牌；若其自身升转回调抛出，不应提前修改
            // 已存在的仙蛊。提交成功后再执行无异步的确定性冲突修复。
            commit();

            ReconcileConflictingXianGu(
                runState,
                candidate
            );
            RegisterXianGuClaimUnsafe(
                candidate,
                runState.TotalFloor
            );
            return true;
        }
    }

    /// <summary>
    /// 在实际永久入牌检查时使用。普通唯一规则保持原行为；仙蛊则使用
    /// 与升转相同的确定性仲裁，避免奖励、合练和转换同时产生同名仙蛊。
    /// </summary>
    internal static bool TryAuthorizePermanentDeckEntry(
        IRunState runState,
        CardModel candidate,
        CardModel? ignoredExistingCard = null
    )
    {
        ArgumentNullException.ThrowIfNull(runState);
        ArgumentNullException.ThrowIfNull(candidate);

        if (!IsXianGu(candidate))
        {
            return CanEnterPermanentDeck(
                runState,
                candidate.Owner,
                candidate,
                ignoredExistingCards:
                    ignoredExistingCard == null
                        ? null
                        : new HashSet<CardModel>
                        {
                            ignoredExistingCard,
                        },
                plannedAdditions: null,
                allowSingleGuReplacementAtCapacity: false
            );
        }

        lock (XianGuMutationSync)
        {
            if (!CanCandidateOwnXianGuClaim(
                    runState,
                    candidate,
                    ignoredCard: ignoredExistingCard
                ))
            {
                return false;
            }

            ReconcileConflictingXianGu(
                runState,
                candidate,
                ignoredExistingCard
            );
            RegisterXianGuClaimUnsafe(
                candidate,
                runState.TotalFloor
            );
            return true;
        }
    }

    internal static void RegisterXianGuClaim(
        CardModel candidate,
        int floor
    )
    {
        ArgumentNullException.ThrowIfNull(candidate);

        if (!IsXianGu(candidate))
        {
            return;
        }

        lock (XianGuMutationSync)
        {
            RegisterXianGuClaimUnsafe(candidate, floor);
        }
    }

    public static bool IsSameCard(
        CardModel first,
        CardModel second
    )
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        return first.Id == second.Id;
    }


    private static bool CanCandidateOwnXianGuClaim(
        IRunState runState,
        CardModel candidate,
        CardModel? ignoredCard
    )
    {
        CardModel[] conflicts = runState.Players
            .SelectMany(player => player.Deck.Cards)
            .Where(existing =>
                !ReferenceEquals(existing, ignoredCard) &&
                !ReferenceEquals(existing, candidate) &&
                IsSameCard(existing, candidate) &&
                IsXianGu(existing)
            )
            .ToArray();

        if (conflicts.Length == 0)
        {
            return true;
        }

        XianGuPriority candidatePriority =
            GetXianGuPriority(runState, candidate);

        return conflicts.All(existing =>
            candidatePriority.CompareTo(
                GetXianGuPriority(runState, existing)
            ) < 0
        );
    }

    private static void ReconcileConflictingXianGu(
        IRunState runState,
        CardModel winner,
        CardModel? ignoredCard = null
    )
    {
        CardModel[] losers = runState.Players
            .SelectMany(player => player.Deck.Cards)
            .Where(existing =>
                !ReferenceEquals(existing, winner) &&
                !ReferenceEquals(existing, ignoredCard) &&
                IsSameCard(existing, winner) &&
                IsXianGu(existing)
            )
            .ToArray();

        foreach (CardModel loser in losers)
        {
            DemoteFromXianGu(loser);
            XianGuClaimFloorState[loser] = 0;

            Entry.Logger.Info(
                $"多人仙蛊唯一性仲裁：保留玩家 " +
                $"{winner.Owner.NetId} 的 {winner.Id}，" +
                $"将玩家 {loser.Owner.NetId} 的同名牌降回五转。"
            );
        }
    }

    private static void DemoteFromXianGu(CardModel card)
    {
        switch (card)
        {
            case AbstractGuZhenRenCard guCard
                when guCard is IGuWormCard:
                guCard.ReconcileGuRankForUniqueness(
                    XianGuRank - 1
                );
                break;

            case AbstractBenMingGuCard benMingGuCard:
                benMingGuCard.ReconcileGuRankForUniqueness(
                    XianGuRank - 1
                );
                break;
        }
    }

    private static void RegisterXianGuClaimUnsafe(
        CardModel card,
        int floor
    )
    {
        if (XianGuClaimFloorState[card] == 0)
        {
            XianGuClaimFloorState[card] =
                Math.Max(0, floor) + 1;
        }
    }

    private static XianGuPriority GetXianGuPriority(
        IRunState runState,
        CardModel card
    )
    {
        int savedFloor = XianGuClaimFloorState[card];
        int claimFloor = savedFloor == 0
            ? int.MinValue
            : savedFloor - 1;

        // 尚未成为仙蛊的候选牌使用当前楼层参与本次仲裁。
        if (!IsXianGu(card))
        {
            claimFloor = runState.TotalFloor;
        }

        int playerSlot = runState.GetPlayerSlotIndex(card.Owner);
        int deckIndex = FindDeckIndex(card.Owner, card);

        return new XianGuPriority(
            claimFloor,
            playerSlot,
            deckIndex,
            card.Owner.NetId
        );
    }

    private static int FindDeckIndex(
        Player player,
        CardModel card
    )
    {
        IReadOnlyList<CardModel> cards = player.Deck.Cards;

        for (int index = 0;
             index < cards.Count;
             index++)
        {
            if (ReferenceEquals(cards[index], card))
            {
                return index;
            }
        }

        return int.MaxValue;
    }

    private readonly record struct XianGuPriority(
        int ClaimFloor,
        int PlayerSlot,
        int DeckIndex,
        ulong PlayerNetId
    ) : IComparable<XianGuPriority>
    {
        public int CompareTo(XianGuPriority other)
        {
            int result = ClaimFloor.CompareTo(other.ClaimFloor);
            if (result != 0)
            {
                return result;
            }

            result = PlayerSlot.CompareTo(other.PlayerSlot);
            if (result != 0)
            {
                return result;
            }

            result = DeckIndex.CompareTo(other.DeckIndex);
            if (result != 0)
            {
                return result;
            }

            return PlayerNetId.CompareTo(other.PlayerNetId);
        }
    }

    private static bool CanEnterPermanentDeck(
        IRunState runState,
        Player receivingPlayer,
        CardModel candidate,
        IReadOnlySet<CardModel>? ignoredExistingCards,
        IReadOnlyList<PlannedCardAddition>? plannedAdditions,
        bool allowSingleGuReplacementAtCapacity = false
    )
    {
        ArgumentNullException.ThrowIfNull(runState);
        ArgumentNullException.ThrowIfNull(receivingPlayer);
        ArgumentNullException.ThrowIfNull(candidate);

        if (receivingPlayer.Character is GuZhenRenCharacter &&
            candidate is IGuWormCard)
        {
            int existingGuCount = receivingPlayer.Deck.Cards.Count(card =>
                card is IGuWormCard &&
                ignoredExistingCards?.Contains(card) != true
            );
            int plannedGuCount = plannedAdditions?.Count(item =>
                ReferenceEquals(item.Player, receivingPlayer) &&
                item.Card is IGuWormCard
            ) ?? 0;

            int guCountBeforeCandidate =
                existingGuCount + plannedGuCount;

            if (guCountBeforeCandidate + 1 >
                GuWormDeckCapacity &&
                !(
                    allowSingleGuReplacementAtCapacity &&
                    guCountBeforeCandidate == GuWormDeckCapacity
                ))
            {
                return false;
            }
        }

        CardUniqueScope scope = GetUniqueScope(candidate);

        if (scope == CardUniqueScope.None)
        {
            return true;
        }

        IEnumerable<CardModel> existingCards =
            scope switch
            {
                CardUniqueScope.PlayerDeck =>
                    receivingPlayer.Deck.Cards,

                CardUniqueScope.EntireRun =>
                    runState.Players.SelectMany(
                        player => player.Deck.Cards
                    ),

                _ => Array.Empty<CardModel>(),
            };

        if (existingCards.Any(existing =>
                ignoredExistingCards?.Contains(existing) != true &&
                Conflicts(scope, candidate, existing)
            ))
        {
            return false;
        }

        if (plannedAdditions == null)
        {
            return true;
        }

        return plannedAdditions.All(planned =>
        {
            if (scope == CardUniqueScope.PlayerDeck &&
                !ReferenceEquals(planned.Player, receivingPlayer))
            {
                return true;
            }

            return !Conflicts(
                scope,
                candidate,
                planned.Card
            );
        });
    }

    private static bool Conflicts(
        CardUniqueScope scope,
        CardModel candidate,
        CardModel existing
    )
    {
        if (!IsSameCard(candidate, existing))
        {
            return false;
        }

        // 仙蛊只与同名仙蛊发生整局冲突。
        if (scope == CardUniqueScope.EntireRun)
        {
            return IsXianGu(existing);
        }

        // 普通唯一在当前玩家牌组内禁止同名牌。
        return true;
    }
}
