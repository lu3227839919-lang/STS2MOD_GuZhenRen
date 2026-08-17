using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Utils;

namespace GuZhenRen.Cards.LiDao;

/// <summary>
/// 新版兽力蛊的实例级永久炼力状态。
///
/// 永久进度主存于卡牌 DynamicVars（随卡牌克隆与存档持久化，跨战斗、
/// 跨幕继承）；普通实例字段仅作兼容桥接。战斗牌增加进度时会同步写回
/// 自己的 DeckVersion，因此同名多张蛊各自独立保存。
/// </summary>
public static class LiDaoBeastTrainingSystem
{
    public const int TrainingRequired = 3;

    /// <summary>
    /// 炼力进度的隐藏 DynamicVar 名（不出现在卡面）。
    /// </summary>
    public const string ProgressVarName = "GuLiTraining";

    /// <summary>
    /// 仅属于当前战斗实例的开战快照。达到 3/3 的同一场战斗中仍保持
    /// false，确保虚影只能从下一场战斗开始生成。
    /// </summary>
    private static readonly SavedAttachedState<CardModel, bool>
        CompletedAtCombatStartState = new(
            Entry.ModId + ".li_dao.beast_training.completed_at_combat_start",
            static () => false
        );

    private static readonly SavedAttachedState<CardModel, bool>
        CombatSnapshotInitializedState = new(
            Entry.ModId + ".li_dao.beast_training.combat_snapshot_initialized",
            static () => false
        );

    /// <summary>
    /// 防止复制、重放或第三方效果让同一具体蛊虫在同一回合重复炼力。
    /// </summary>
    private static readonly SavedAttachedState<CardModel, int>
        LastEffectiveActivationTurnState = new(
            Entry.ModId + ".li_dao.beast_training.last_activation_turn",
            static () => 0
        );

    public static int GetProgress(CardModel card)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (card is not AbstractLiDaoBeastGuCard beastGu)
        {
            return 0;
        }

        int progress = ReadProgress(card);
        beastGu.BeastTrainingProgressBridge = progress;
        return progress;
    }

    public static bool IsTrainingComplete(CardModel card) =>
        GetProgress(card) >= TrainingRequired;

    public static bool WasCompleteAtCombatStart(CardModel card) =>
        card is AbstractLiDaoBeastGuCard &&
        CombatSnapshotInitializedState[card] &&
        CompletedAtCombatStartState[card];

    /// <summary>
    /// 仅在真正的新战斗初始化时调用。QuickSL 已保存的战斗实例不会覆盖
    /// 既有快照，避免把本战刚达到的 3/3 误判成开战时已完成。
    /// </summary>
    internal static void CaptureCombatStart(CardModel card)
    {
        if (card is not AbstractLiDaoBeastGuCard ||
            CombatSnapshotInitializedState[card])
        {
            return;
        }

        CompletedAtCombatStartState[card] = IsTrainingComplete(card);
        CombatSnapshotInitializedState[card] = true;
        LastEffectiveActivationTurnState[card] = 0;
    }

    /// <summary>
    /// 战斗中临时生成的兽力蛊不属于开战时已有的蛊，即使从某个已炼成
    /// 实例复制而来，本战也不能立即获得虚影。
    /// </summary>
    internal static void InitializeGeneratedForCurrentCombat(CardModel card)
    {
        if (card is not AbstractLiDaoBeastGuCard ||
            CombatSnapshotInitializedState[card])
        {
            return;
        }

        CompletedAtCombatStartState[card] = false;
        CombatSnapshotInitializedState[card] = true;
        LastEffectiveActivationTurnState[card] = 0;
    }

    /// <summary>
    /// 登记一次有效催动。返回 true 表示该蛊在本场开战时已经炼成，
    /// 因而本次催动可以尝试生成虚影。
    /// </summary>
    internal static bool RecordEffectiveActivation(
        AbstractLiDaoBeastGuCard card,
        CardPlay cardPlay
    )
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(cardPlay);

        if (!cardPlay.IsFirstInSeries)
        {
            return false;
        }

        if (!CombatSnapshotInitializedState[card])
        {
            InitializeGeneratedForCurrentCombat(card);
        }

        int turn = card.Owner.PlayerCombatState?.TurnNumber ?? 1;
        if (LastEffectiveActivationTurnState[card] == turn)
        {
            return false;
        }

        LastEffectiveActivationTurnState[card] = turn;
        int progress = GetProgress(card);
        if (progress < TrainingRequired)
        {
            SetProgress(card, progress + 1);
            return false;
        }

        return CompletedAtCombatStartState[card];
    }

    private static void SetProgress(
        AbstractLiDaoBeastGuCard card,
        int progress
    )
    {
        int normalized = Math.Clamp(progress, 0, TrainingRequired);
        WriteProgress(card, normalized);

        if (card.DeckVersion is AbstractLiDaoBeastGuCard deckCard &&
            !ReferenceEquals(deckCard, card))
        {
            WriteProgress(deckCard, normalized);
        }
    }

    private static int ReadProgress(CardModel card)
    {
        if (card.DynamicVars.TryGetValue(
                ProgressVarName,
                out DynamicVar? progressVar) &&
            progressVar is not null)
        {
            return Math.Clamp(progressVar.IntValue, 0, TrainingRequired);
        }

        return 0;
    }

    private static void WriteProgress(
        AbstractLiDaoBeastGuCard card,
        int progress
    )
    {
        card.BeastTrainingProgressBridge = progress;
        if (card.DynamicVars.TryGetValue(
                ProgressVarName,
                out DynamicVar? progressVar) &&
            progressVar is not null)
        {
            progressVar.BaseValue = progress;
        }
    }
}
