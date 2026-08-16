using GuZhenRen.Multiplayer;
using GuZhenRen.Powers.LiDao;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
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

    /// <summary>
    /// 战斗开始生成的伴生牌与药水随机给予力道蛊时生成的伴生牌可以
    /// 参与炼力；其他战斗内复制或随机生成的临时伴生牌仍然不计数。
    /// </summary>
    private static readonly SavedAttachedState<CardModel, bool>
        TemporaryTrainingCompanionState = new(
            Entry.ModId + ".li_dao.temporary_training_companion",
            static () => false
        );

    private static readonly SavedAttachedState<CardModel, int>
        ExtraTrainingState = new(
            Entry.ModId + ".li_dao.extra_training",
            static () => 0
        );

    internal static void ResetForCombat(CardModel card)
    {
        if (card is not ILiDaoTrainingGuCard)
        {
            return;
        }

        ProgressState[card] = 0;
        UnsealedState[card] = false;
        ExtraTrainingState[card] = 0;
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

    internal static void MarkCompanionCanTrain(
        CardModel companion
    )
    {
        ArgumentNullException.ThrowIfNull(companion);
        TemporaryTrainingCompanionState[companion] = true;
    }

    /// <summary>
    /// 一张系统生成的炼力伴生牌完成其首次 CardPlay 后调用。
    /// 普通临时生成牌没有授权标记，Replay 后续段也不是
    /// IsFirstInSeries，因此均不计数。
    /// </summary>
    public static async Task TrainFromCompanionAsync(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        Type guType
    )
    {
        ArgumentNullException.ThrowIfNull(cardPlay);
        ArgumentNullException.ThrowIfNull(guType);

        CardModel companion = cardPlay.Card;
        if (!cardPlay.IsFirstInSeries ||
            !TemporaryTrainingCompanionState[companion] ||
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
            await RecordExtraTrainingAsync(
                choiceContext,
                cardPlay,
                companion.Owner,
                guType
            );
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

        PileType destination = GuCardPileSystem
            .HasAvailableActiveSlot(companion.Owner)
                ? GuCardPileSystem.PileType
                : GuCardPileSystem.StoragePileType;

        await GuCardPileSystem.MoveCardToPileAsync(
            sealedGu,
            destination,
            skipVisuals: false
        );
    }

    private static async Task RecordExtraTrainingAsync(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        Player owner,
        Type guType
    )
    {
        CardModel? card = FindGuCard(owner, guType);
        if (card is not ILiDaoExtraTrainingGuCard || !IsUnsealed(card))
        {
            return;
        }

        int remainder = ExtraTrainingState[card] + 1;
        QunLiPower? power = owner.Creature.GetPower<QunLiPower>();
        if (power == null)
        {
            ExtraTrainingState[card] = remainder;
            return;
        }

        while (remainder >= 2)
        {
            remainder -= 2;
            await PowerCmd.ModifyAmount(
                choiceContext,
                power,
                1,
                owner.Creature,
                cardPlay.Card
            );
        }

        ExtraTrainingState[card] = remainder;
    }

    internal static async Task FlushExtraTrainingAsync(
        PlayerChoiceContext choiceContext,
        QunLiGu source
    )
    {
        CardModel? card = FindGuCard(source.Owner, typeof(QunLiGu));
        if (card == null)
        {
            return;
        }

        int groups = ExtraTrainingState[card] / 2;
        ExtraTrainingState[card] %= 2;
        if (groups <= 0)
        {
            return;
        }

        QunLiPower? power = source.Owner.Creature.GetPower<QunLiPower>();
        if (power == null)
        {
            ExtraTrainingState[card] += groups * 2;
            return;
        }

        await PowerCmd.ModifyAmount(
            choiceContext,
            power,
            groups,
            source.Owner.Creature,
            source
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

    private static CardModel? FindGuCard(Player owner, Type guType) =>
        GuCardPileSystem.PileType.GetPile(owner).Cards
            .Concat(GuCardPileSystem.StoragePileType.GetPile(owner).Cards)
            .Concat(GuCardPileSystem.RecoveryPileType.GetPile(owner).Cards)
            .Concat(GuCardPileSystem.GuSealedPileType.GetPile(owner).Cards)
            .Where(card => card.GetType() == guType)
            .OrderBy(GuZhenRenDeterminism.GetCardNetworkId)
            .FirstOrDefault();
}
