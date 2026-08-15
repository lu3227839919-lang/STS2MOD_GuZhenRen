namespace GuZhenRen.Aperture;

/// <summary>
/// 仙元发放事务状态。
/// </summary>
public enum ApertureEssenceGrantState
{
    NotStarted,
    InProgress,
    Completed,
}

/// <summary>
/// RitsuLib 按玩家保存并随运行快照同步的空窍/仙窍数据。
/// 战斗内一次性标记也保存在这里，以支持断线重连和中途恢复。
/// </summary>
public sealed class ApertureRunData
{
    public int Xp { get; set; }

    public int Rank { get; set; } = ApertureProgression.MinimumRank;

    /// <summary>
    /// 九转是当前已实现终点。
    /// </summary>
    public bool IsCultivationComplete { get; set; }

    /// <summary>
    /// 当前战斗所在的运行层数。用于区分真正的新战斗与同一战斗的重连恢复。
    /// </summary>
    public int ActiveCombatFloor { get; set; } = -1;

    /// <summary>
    /// 已结算胜利修为的最后一个运行层数，防止胜利回调重放时重复加修为。
    /// </summary>
    public int VictoryXpAppliedFloor { get; set; } = -1;

    /// <summary>
    /// 当前战斗仙元发放事务状态。
    /// </summary>
    public ApertureEssenceGrantState EssenceGrantState { get; set; }

    /// <summary>
    /// 已成功结算最大生命奖励的最高转数。
    /// </summary>
    public int MaxHpAppliedThroughRank { get; set; }

    /// <summary>
    /// 正在结算最大生命奖励的转数，以及命令执行前的最大生命。
    /// 两者共同用于识别“命令已成功、进度标记尚未提交”的重连窗口。
    /// </summary>
    public int MaxHpAwardInProgressRank { get; set; }

    public int MaxHpBeforePendingAward { get; set; }

    /// <summary>
    /// 已完成扩展通知的最高转数。
    /// </summary>
    public int RankAdvanceNotifiedThroughRank { get; set; }

    /// <summary>
    /// 用于兼容旧存档：首次读取旧数据时，把副作用进度视为已完成到当前转数，
    /// 防止更新模组后重复获得历史最大生命奖励。
    /// </summary>
    public bool SideEffectProgressInitialized { get; set; }

    /// <summary>
    /// 已提交转数、但副作用尚未全部完成的突破。
    /// </summary>
    public int PendingRankAdvanceFrom { get; set; }

    public int PendingRankAdvanceTo { get; set; }

    /// <summary>
    /// 已发放“杀招推演”牌的战斗层数（-1 表示本场尚未发放）。
    /// 空窍三转起每场战斗开始时发放一张。
    /// </summary>
    public int ShaZhaoDerivationGrantFloor { get; set; } = -1;

    /// <summary>
    /// 当前战斗已完成的杀招推演次数。
    /// 三至七转每场最多 1 次，八至九转最多 2 次。
    /// </summary>
    public int ShaZhaoDerivationsThisCombat { get; set; }

    public bool NeedsNormalization()
    {
        int normalizedRank = Math.Clamp(
            Rank,
            ApertureProgression.MinimumRank,
            ApertureProgression.MaximumImplementedRank
        );

        if (Rank != normalizedRank ||
            Xp < 0 ||
            ActiveCombatFloor < -1 ||
            VictoryXpAppliedFloor < -1)
        {
            return true;
        }

        bool shouldBeComplete =
            normalizedRank >= ApertureProgression.MaximumImplementedRank;

        if (IsCultivationComplete != shouldBeComplete ||
            (shouldBeComplete && Xp != 0) ||
            !Enum.IsDefined(typeof(ApertureEssenceGrantState), EssenceGrantState) ||
            !SideEffectProgressInitialized)
        {
            return true;
        }

        int minimumAppliedRank =
            normalizedRank < ApertureProgression.ImmortalRank
                ? normalizedRank
                : ApertureProgression.MinimumRank;

        if (MaxHpAppliedThroughRank < minimumAppliedRank ||
            MaxHpAppliedThroughRank > normalizedRank ||
            RankAdvanceNotifiedThroughRank < ApertureProgression.MinimumRank ||
            RankAdvanceNotifiedThroughRank > normalizedRank)
        {
            return true;
        }

        bool hasMaxHpAwardInProgress =
            MaxHpAwardInProgressRank != 0 ||
            MaxHpBeforePendingAward != 0;

        if (hasMaxHpAwardInProgress &&
            (MaxHpAwardInProgressRank < ApertureProgression.ImmortalRank ||
             MaxHpAwardInProgressRank <= MaxHpAppliedThroughRank ||
             MaxHpAwardInProgressRank > normalizedRank ||
             MaxHpBeforePendingAward <= 0))
        {
            return true;
        }

        bool hasPending =
            PendingRankAdvanceFrom > 0 ||
            PendingRankAdvanceTo > 0;

        return hasPending &&
               (PendingRankAdvanceFrom < ApertureProgression.MinimumRank ||
                PendingRankAdvanceTo <= PendingRankAdvanceFrom ||
                PendingRankAdvanceTo > normalizedRank);
    }

    public void Normalize()
    {
        Rank = Math.Clamp(
            Rank,
            ApertureProgression.MinimumRank,
            ApertureProgression.MaximumImplementedRank
        );
        Xp = Math.Max(0, Xp);
        ActiveCombatFloor = Math.Max(-1, ActiveCombatFloor);
        VictoryXpAppliedFloor = Math.Max(-1, VictoryXpAppliedFloor);
        ShaZhaoDerivationGrantFloor = Math.Max(
            -1,
            ShaZhaoDerivationGrantFloor
        );
        ShaZhaoDerivationsThisCombat = Math.Max(
            0,
            ShaZhaoDerivationsThisCombat
        );

        if (Rank >= ApertureProgression.MaximumImplementedRank)
        {
            Rank = ApertureProgression.MaximumImplementedRank;
            Xp = 0;
            IsCultivationComplete = true;
        }
        else
        {
            IsCultivationComplete = false;
        }

        if (!Enum.IsDefined(
                typeof(ApertureEssenceGrantState),
                EssenceGrantState
            ))
        {
            EssenceGrantState = ApertureEssenceGrantState.NotStarted;
        }

        if (!SideEffectProgressInitialized)
        {
            // 旧存档没有进度字段。假定历史转数的副作用已经结算，
            // 避免升级模组后重复加最大生命或重复播放主题。
            MaxHpAppliedThroughRank = Rank;
            MaxHpAwardInProgressRank = 0;
            MaxHpBeforePendingAward = 0;
            RankAdvanceNotifiedThroughRank = Rank;
            SideEffectProgressInitialized = true;
        }

        if (Rank < ApertureProgression.ImmortalRank)
        {
            // 凡人阶段没有最大生命奖励，可直接视为完成到当前转数。
            MaxHpAppliedThroughRank = Rank;
        }
        else
        {
            MaxHpAppliedThroughRank = Math.Clamp(
                MaxHpAppliedThroughRank,
                ApertureProgression.MinimumRank,
                Rank
            );
        }

        RankAdvanceNotifiedThroughRank = Math.Clamp(
            RankAdvanceNotifiedThroughRank,
            ApertureProgression.MinimumRank,
            Rank
        );

        bool validMaxHpAwardInProgress =
            MaxHpAwardInProgressRank >= ApertureProgression.ImmortalRank &&
            MaxHpAwardInProgressRank > MaxHpAppliedThroughRank &&
            MaxHpAwardInProgressRank <= Rank &&
            MaxHpBeforePendingAward > 0;

        if (!validMaxHpAwardInProgress)
        {
            MaxHpAwardInProgressRank = 0;
            MaxHpBeforePendingAward = 0;
        }

        bool validPending =
            PendingRankAdvanceFrom >= ApertureProgression.MinimumRank &&
            PendingRankAdvanceTo > PendingRankAdvanceFrom &&
            PendingRankAdvanceTo <= Rank;

        if (!validPending)
        {
            PendingRankAdvanceFrom = 0;
            PendingRankAdvanceTo = 0;
        }
    }
}
