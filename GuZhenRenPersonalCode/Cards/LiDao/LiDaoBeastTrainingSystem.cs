using GuZhenRen.Multiplayer;

using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Utils;

namespace GuZhenRen.Cards.LiDao;

/// <summary>
/// 兽力蛊的战斗内炼力系统。每场战斗独立记录每张具体兽力蛊的
/// 0/3 进度；对应伴生牌完成首次有效结算后，按稳定顺序逐只推进。
/// </summary>
public static class LiDaoBeastTrainingSystem
{
    public const int TrainingRequired = 3;

    private static readonly SavedAttachedState<CardModel, int>
        TrainingProgressState = new(
            Entry.ModId + ".li_dao.beast_training.combat_progress",
            static () => 0
        );

    private static readonly SavedAttachedState<CardModel, bool>
        TrainingUnlockedState = new(
            Entry.ModId + ".li_dao.beast_training.combat_unlocked",
            static () => false
        );

    private static readonly SavedAttachedState<CardModel, bool>
        CombatInitializedState = new(
            Entry.ModId + ".li_dao.beast_training.combat_initialized_v2",
            static () => false
        );

    /// <summary>
    /// 同一卡牌类型既可能是开战生成的正常伴生牌，也可能是普通临时牌。
    /// 资格必须附着在具体战斗实例上，并进入 QuickSL/多人快照。
    /// </summary>
    private static readonly SavedAttachedState<CardModel, bool>
        CanTrainBeastGuState = new(
            Entry.ModId + ".li_dao.beast_training.companion_can_train",
            static () => false
        );

    public static int GetProgress(CardModel card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return card is ILiDaoBeastGuCard
            ? Math.Clamp(
                TrainingProgressState[card],
                0,
                TrainingRequired
            )
            : 0;
    }

    public static bool IsUnlocked(CardModel card) =>
        card is ILiDaoBeastGuCard &&
        TrainingUnlockedState[card];

    public static bool IsTrainingSealed(CardModel card) =>
        card is ILiDaoBeastGuCard &&
        GuSealSystem.IsTrainingSealed(card);

    /// <summary>
    /// 新战斗只初始化一次。QuickSL 已恢复的战斗实例会保留当前进度、
    /// 解封状态和所在牌堆；下一场的新战斗克隆则重新从 0/3 开始。
    /// </summary>
    internal static void InitializeForCombat(CardModel card)
    {
        if (card is not ILiDaoBeastGuCard ||
            CombatInitializedState[card])
        {
            return;
        }

        ResetForCombat(card);
    }

    /// <summary>
    /// 战斗中临时生成或复制的兽力蛊默认不继承来源实例的解封状态。
    /// </summary>
    internal static void InitializeGeneratedForCurrentCombat(CardModel card)
    {
        if (card is ILiDaoBeastGuCard)
        {
            ResetForCombat(card);
        }
    }

    internal static void ResetForCombat(CardModel card)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (card is not ILiDaoBeastGuCard)
        {
            return;
        }

        TrainingProgressState[card] = 0;
        TrainingUnlockedState[card] = false;
        CombatInitializedState[card] = true;
        GuSealSystem.SealForTraining(card);
    }

    /// <summary>
    /// 显式允许一张临时伴生牌炼力（当前用于药水生成的伴生牌）。
    /// 普通临时牌保持默认 false。
    /// </summary>
    internal static void AllowCompanionTraining(CardModel companion)
    {
        ArgumentNullException.ThrowIfNull(companion);
        if (companion is ILiDaoCompanionCard)
        {
            CanTrainBeastGuState[companion] = true;
        }
    }

    internal static bool CanTrainBeastGu(CardModel companion) =>
        companion is ILiDaoCompanionCard &&
        CanTrainBeastGuState[companion];

    /// <summary>
    /// 一张伴生牌完整结算后的统一入口。Replay 后续 CardPlay、无资格的
    /// 临时牌以及已全部解封的类型都会被幂等忽略。
    /// </summary>
    internal static async Task<bool> RecordCompanionPlayAsync(
        CardModel companion,
        CardPlay cardPlay
    )
    {
        ArgumentNullException.ThrowIfNull(companion);
        ArgumentNullException.ThrowIfNull(cardPlay);

        if (companion is not ILiDaoCompanionCard companionCard ||
            !ReferenceEquals(cardPlay.Card, companion) ||
            !cardPlay.IsFirstInSeries ||
            !CanTrainBeastGu(companion))
        {
            return false;
        }

        AbstractLiDaoBeastGuCard? target =
            FindCurrentTrainingTarget(
                cardPlay.Player,
                companionCard.SourceGuType
            );
        if (target == null)
        {
            return false;
        }

        return await AdvanceTrainingAsync(target);
    }

    internal static AbstractLiDaoBeastGuCard?
        FindCurrentTrainingTarget(Player owner, Type sourceGuType)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(sourceGuType);

        return GuCardPileSystem.GuSealedPileType
            .GetPile(owner)
            .Cards
            .OfType<AbstractLiDaoBeastGuCard>()
            .Where(card =>
                card.GetType() == sourceGuType &&
                !IsUnlocked(card) &&
                IsTrainingSealed(card)
            )
            .OrderBy(GuZhenRenDeterminism.GetDeckCardIndex)
            .ThenBy(GuZhenRenDeterminism.GetCardNetworkId)
            .ThenBy(
                static card => card.Id.ToString(),
                StringComparer.Ordinal
            )
            .FirstOrDefault();
    }

    private static async Task<bool> AdvanceTrainingAsync(
        AbstractLiDaoBeastGuCard beastGu
    )
    {
        if (IsUnlocked(beastGu) || !IsTrainingSealed(beastGu))
        {
            return false;
        }

        int progress = Math.Min(
            TrainingRequired,
            GetProgress(beastGu) + 1
        );
        TrainingProgressState[beastGu] = progress;
        beastGu.Pile?.InvokeContentsChanged();

        Entry.Logger.Info(
            $"[炼力] {beastGu.Id}#" +
            $"{GuZhenRenDeterminism.GetCardNetworkId(beastGu)} " +
            $"进度 {progress}/{TrainingRequired}。"
        );

        if (progress < TrainingRequired)
        {
            return true;
        }

        TrainingUnlockedState[beastGu] = true;
        await GuCardPileSystem.ReleaseTrainingSealedGuAsync(
            beastGu,
            beastGu.Owner
        );
        Entry.Logger.Info(
            $"[炼力完成] {beastGu.Id}#" +
            $"{GuZhenRenDeterminism.GetCardNetworkId(beastGu)} " +
            "已解封并进入蛊存放队列。"
        );
        return true;
    }
}
