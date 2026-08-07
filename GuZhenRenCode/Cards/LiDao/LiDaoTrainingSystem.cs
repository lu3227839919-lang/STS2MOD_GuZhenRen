using GuZhenRen.Multiplayer;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Utils;

namespace GuZhenRen.Cards.LiDao;

/// <summary>
/// 力道蛊的战斗内“练力—解封”状态。进度保存于具体战斗卡实例，
/// 因而同名多只蛊会按稳定顺序逐只练成，不会共享或串档。
/// </summary>
public static class LiDaoTrainingSystem
{
    private static readonly SavedAttachedState<CardModel, int>
        ProgressState = new(
            Entry.ModId + ".li_dao.training_progress",
            static () => 0
        );

    private static readonly SavedAttachedState<CardModel, bool>
        UnsealedState = new(
            Entry.ModId + ".li_dao.unsealed",
            static () => false
        );

    internal static void ResetForCombat(CardModel card)
    {
        if (card is not ILiDaoTrainingGuCard)
        {
            return;
        }

        ProgressState[card] = 0;
        UnsealedState[card] = false;
        GuCardUsageRules.ResetUses(card);
    }

    public static int GetProgress(CardModel card) =>
        card is ILiDaoTrainingGuCard ? ProgressState[card] : 0;

    public static bool IsUnsealed(CardModel card) =>
        card is ILiDaoTrainingGuCard && UnsealedState[card];

    public static bool IsSealed(CardModel card) =>
        card is ILiDaoTrainingGuCard &&
        !UnsealedState[card] &&
        card.Pile?.Type == GuCardPileSystem.GuSealedPileType;

    /// <summary>
    /// 一张永久伴生牌完成其首次 CardPlay 后调用。临时生成牌没有
    /// DeckVersion，Replay 后续段也不是 IsFirstInSeries，因此均不计数。
    /// </summary>
    public static async Task TrainFromCompanionAsync(
        CardPlay cardPlay,
        Type guType
    )
    {
        ArgumentNullException.ThrowIfNull(cardPlay);
        ArgumentNullException.ThrowIfNull(guType);

        CardModel companion = cardPlay.Card;
        if (!cardPlay.IsFirstInSeries ||
            companion.DeckVersion == null ||
            companion.Owner.PlayerCombatState == null)
        {
            return;
        }

        CardModel? sealedGu = GuCardPileSystem
            .GuSealedPileType
            .GetPile(companion.Owner)
            .Cards
            .Where(card =>
                card.GetType() == guType &&
                card is ILiDaoTrainingGuCard &&
                !UnsealedState[card]
            )
            .OrderBy(GuZhenRenDeterminism.GetCardNetworkId)
            .FirstOrDefault();

        if (sealedGu is not ILiDaoTrainingGuCard trainingGu)
        {
            return;
        }

        int required = Math.Max(1, trainingGu.TrainingRequired);
        int progress = Math.Min(required, ProgressState[sealedGu] + 1);
        ProgressState[sealedGu] = progress;

        if (progress < required)
        {
            return;
        }

        UnsealedState[sealedGu] = true;
        GuCardUsageRules.ResetUses(sealedGu);

        int activeCount = GuCardPileSystem.PileType
            .GetPile(companion.Owner)
            .Cards
            .Count(static card => card is IGuWormCard);

        PileType destination = activeCount <
            GuCardPileSystem.ActivePileCapacity
                ? GuCardPileSystem.PileType
                : GuCardPileSystem.RecoveryPileType;

        await GuCardPileSystem.MoveCardToPileAsync(
            sealedGu,
            destination,
            skipVisuals: false
        );
    }

    internal static IEnumerable<string> GetSealedProgressLines(
        Player owner
    )
    {
        ArgumentNullException.ThrowIfNull(owner);

        return GuCardPileSystem.GuSealedPileType
            .GetPile(owner)
            .Cards
            .OfType<ILiDaoTrainingGuCard>()
            .Cast<CardModel>()
            .OrderBy(card => card.Title, StringComparer.Ordinal)
            .ThenBy(GuZhenRenDeterminism.GetCardNetworkId)
            .Select(card =>
            {
                ILiDaoTrainingGuCard gu = (ILiDaoTrainingGuCard)card;
                return $"{card.Title} {ProgressState[card]}/" +
                    Math.Max(1, gu.TrainingRequired);
            });
    }
}
