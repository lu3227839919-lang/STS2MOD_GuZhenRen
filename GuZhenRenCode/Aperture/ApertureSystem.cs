using System.Collections.Concurrent;

using GuZhenRen.Cards;
using GuZhenRen.Cards.ImmortalEssence;
using GuZhenRen.Cards.ShaZhao;
using GuZhenRen.Relics;

using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

using STS2RitsuLib;
using STS2RitsuLib.RunData;

namespace GuZhenRen.Aperture;

/// <summary>
/// 暂不包含灾劫与十转的空窍/仙窍运行时。
/// 所有需要跨保存、重连和多人快照恢复的状态均写入 RitsuLib Run Saved Data。
/// </summary>
public static class ApertureSystem
{
    private const string SavedDataKey = "aperture";
    private static readonly object SyncRoot = new();
    private static readonly ConcurrentDictionary<
        PlayerRunKey,
        SemaphoreSlim
    > EssenceGrantLocks = new();
    private static readonly ConcurrentDictionary<
        PlayerRunKey,
        SemaphoreSlim
    > RankAdvanceLocks = new();

    private static PlayerRunSavedData<ApertureRunData>? _savedData;
    private static bool _initialized;

    public static bool IsInitialized =>
        _initialized && _savedData != null;

    public static void Initialize()
    {
        lock (SyncRoot)
        {
            if (_initialized)
            {
                return;
            }

            if (_savedData == null)
            {
                using (RitsuLibFramework.BeginModDataRegistration(Entry.ModId))
                {
                    _savedData = RitsuLibFramework
                        .GetRunSavedDataStore(Entry.ModId)
                        .RegisterPerPlayer<ApertureRunData>(
                            SavedDataKey,
                            static () => new ApertureRunData(),
                            options: new RunSavedDataOptions
                            {
                                SchemaVersion = 1,
                            }
                        );
                }
            }

            _initialized = true;
            Entry.Logger.Info(
                "空窍/仙窍运行时初始化完成（多人状态已接入运行快照）。"
            );
        }
    }

    public static void Uninitialize()
    {
        lock (SyncRoot)
        {
            _initialized = false;
            EssenceGrantLocks.Clear();
            RankAdvanceLocks.Clear();
        }
    }

    public static ApertureRunData GetState(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        EnsureAvailable();

        ApertureRunData data = _savedData!.Get(player);
        if (!data.NeedsNormalization())
        {
            return data;
        }

        return _savedData.Modify(
            player,
            static value => value.Normalize()
        );
    }

    /// <summary>
    /// 每场战斗开始时以一次原子修改重置仙元发放事务。
    /// </summary>
    internal static void HandleCombatStarting(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        EnsureAvailable();

        if (!HasAperture(player))
        {
            return;
        }

        int currentFloor = player.RunState.TotalFloor;

        _savedData!.Modify(
            player,
            data =>
            {
                data.Normalize();

                // BeforeCombatStart 可能在同一战斗的重连恢复中再次触发。
                // 只有层数变化时才开始新的战斗事务，避免重复发放仙元。
                if (data.ActiveCombatFloor == currentFloor)
                {
                    return;
                }

                data.ActiveCombatFloor = currentFloor;
                data.EssenceGrantState =
                    ApertureEssenceGrantState.NotStarted;
                data.ShaZhaoDerivationGrantFloor = -1;
                data.ShaZhaoDerivationsThisCombat = 0;
            }
        );

        RefreshRelicVisualState(player);
    }

    /// <summary>
    /// 空窍三转起，每场战斗开始时把“杀招推演”直接加入手牌。
    /// 不占用起手抽牌；同一战斗层数只发放一次（重连安全）。
    /// </summary>
    internal static async Task HandleShaZhaoDerivationGrantAsync(
        Player player
    )
    {
        ArgumentNullException.ThrowIfNull(player);
        EnsureAvailable();

        if (!HasAperture(player))
        {
            return;
        }

        ApertureRunData data = GetState(player);
        if (data.Rank <
            ApertureProgression.ShaZhaoDerivationUnlockRank)
        {
            return;
        }

        int currentFloor = player.RunState.TotalFloor;
        if (data.ShaZhaoDerivationGrantFloor == currentFloor)
        {
            return;
        }

        if (player.PlayerCombatState is not { } combatState)
        {
            return;
        }

        CardModel derivation = player
            .Creature
            .CombatState!
            .CreateCard(
                ModelDb.Card<ShaZhaoTuiYan>(),
                player
            );

        await CardPileCmd.AddGeneratedCardToCombat(
            derivation,
            PileType.Hand,
            player
        );

        _savedData!.Modify(
            player,
            d =>
            {
                d.Normalize();
                if (d.ShaZhaoDerivationGrantFloor !=
                    currentFloor)
                {
                    d.ShaZhaoDerivationGrantFloor =
                        currentFloor;
                }
            }
        );
    }

    /// <summary>
    /// 推演成功后登记次数；八至九转每场最多 2 次，
    /// 第一次成功后再把第二张“杀招推演”放入弃牌堆。
    /// </summary>
    internal static async Task RegisterShaZhaoDerivationAsync(
        Player player
    )
    {
        ArgumentNullException.ThrowIfNull(player);
        EnsureAvailable();

        if (!HasAperture(player))
        {
            return;
        }

        ApertureRunData data = GetState(player);
        if (data.Rank <
            ApertureProgression.ShaZhaoDerivationUnlockRank)
        {
            return;
        }

        int completed = data.ShaZhaoDerivationsThisCombat + 1;

        _savedData!.Modify(
            player,
            d =>
            {
                d.Normalize();
                d.ShaZhaoDerivationsThisCombat = Math.Max(
                    0,
                    completed
                );
            }
        );

        // 八至九转：第二次推演由第二张推演牌提供。
        if (data.Rank >=
                ApertureProgression.ShaZhaoDerivationSecondRank &&
            completed <
                ApertureProgression.ShaZhaoDerivationMaxPerCombat &&
            player.PlayerCombatState is { })
        {
            CardModel second = player
                .Creature
                .CombatState!
                .CreateCard(
                    ModelDb.Card<ShaZhaoTuiYan>(),
                    player
                );
            await AddShaZhaoDerivationToDiscardAsync(
                player,
                second
            );
        }
    }

    private static async Task AddShaZhaoDerivationToDiscardAsync(
        Player player,
        CardModel second
    )
    {
        await CardPileCmd.AddGeneratedCardToCombat(
            second,
            PileType.Discard,
            player
        );
    }

    /// <summary>
    /// 房间进入时恢复此前已提交但未完成的升转副作用。
    /// </summary>
    internal static async Task HandleRoomEnteredAsync(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        EnsureAvailable();

        if (!HasAperture(player))
        {
            return;
        }

        await ResumePendingRankAdvanceAsync(player);
        RefreshRelicVisualState(player);
    }

    /// <summary>
    /// 在每场战斗第一次抽初始手牌前发放一张对应仙元牌。
    /// 发放使用 NotStarted -> InProgress -> Completed 事务，失败时回滚为可重试。
    /// </summary>
    internal static async Task HandleBeforeHandDrawAsync(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        EnsureAvailable();

        if (!HasAperture(player))
        {
            return;
        }

        await ResumePendingRankAdvanceAsync(player);

        PlayerRunKey key = new(
            player.RunState,
            player.NetId
        );
        SemaphoreSlim gate = EssenceGrantLocks.GetOrAdd(
            key,
            static _ => new SemaphoreSlim(1, 1)
        );

        await gate.WaitAsync();
        try
        {
            // 如果上一次在命令成功后、提交 Completed 前中断，
            // 手牌中的仙元就是事务已经成功的证据；否则回滚并重试。
            ApertureRunData existing = GetState(player);
            if (existing.EssenceGrantState ==
                ApertureEssenceGrantState.InProgress)
            {
                bool essenceAlreadyPresent =
                    player.PlayerCombatState?.Hand.Cards
                        .Any(card => card is AbstractXianYuanCard) == true;

                _savedData!.Modify(
                    player,
                    data =>
                    {
                        data.Normalize();
                        if (data.EssenceGrantState ==
                            ApertureEssenceGrantState.InProgress)
                        {
                            data.EssenceGrantState = essenceAlreadyPresent
                                ? ApertureEssenceGrantState.Completed
                                : ApertureEssenceGrantState.NotStarted;
                        }
                    }
                );
            }

            bool shouldGrant = false;
            int rank = ApertureProgression.MinimumRank;

            _savedData!.Modify(
                player,
                data =>
                {
                    data.Normalize();
                    if (data.EssenceGrantState !=
                        ApertureEssenceGrantState.NotStarted)
                    {
                        return;
                    }

                    data.EssenceGrantState =
                        ApertureEssenceGrantState.InProgress;
                    rank = data.Rank;
                    shouldGrant = true;
                }
            );

            if (!shouldGrant)
            {
                return;
            }

            try
            {
                CardModel? essence = CreateImmortalEssence(rank, player);
                if (essence != null)
                {
                    await CardPileCmd.AddGeneratedCardToCombat(
                        essence,
                        PileType.Hand,
                        player
                    );
                }

                _savedData.Modify(
                    player,
                    data =>
                    {
                        data.Normalize();
                        data.EssenceGrantState =
                            ApertureEssenceGrantState.Completed;
                    }
                );
            }
            catch
            {
                _savedData.Modify(
                    player,
                    data =>
                    {
                        data.Normalize();
                        if (data.EssenceGrantState ==
                            ApertureEssenceGrantState.InProgress)
                        {
                            data.EssenceGrantState =
                                ApertureEssenceGrantState.NotStarted;
                        }
                    }
                );
                throw;
            }
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// 胜利后先提交进度和“待完成突破”，再以幂等步骤执行副作用。
    /// 断线后可由房间进入或初始抽牌恢复。
    /// </summary>
    internal static async Task HandleCombatVictoryAsync(
        Player player,
        CombatRoom room
    )
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(room);
        EnsureAvailable();

        if (!HasAperture(player))
        {
            return;
        }

        ApertureTransition transition = default;
        int currentFloor = player.RunState.TotalFloor;

        _savedData!.Modify(
            player,
            data =>
            {
                data.Normalize();

                // 胜利回调可能在网络恢复或房间重建中重放。
                // 层数标记和修为变更在同一次 Modify 中提交，避免重复结算。
                if (data.VictoryXpAppliedFloor == currentFloor)
                {
                    return;
                }

                data.VictoryXpAppliedFloor = currentFloor;
                transition = ApertureProgression.GainVictoryXp(
                    data,
                    GetVictoryXp(room)
                );

                if (transition.RankChanged)
                {
                    if (data.PendingRankAdvanceFrom <= 0)
                    {
                        data.PendingRankAdvanceFrom =
                            transition.PreviousRank;
                    }
                    else
                    {
                        data.PendingRankAdvanceFrom = Math.Min(
                            data.PendingRankAdvanceFrom,
                            transition.PreviousRank
                        );
                    }

                    data.PendingRankAdvanceTo = Math.Max(
                        data.PendingRankAdvanceTo,
                        transition.CurrentRank
                    );
                }
            }
        );

        // 即使这是重复胜利回调，也可能还有上次未完成的副作用需要恢复。
        await ResumePendingRankAdvanceAsync(player);

        RefreshRelicVisualState(player);
    }

    internal static void RefreshRelicVisualState(Player player)
    {
        KongQiaoRelic? relic = FindApertureRelic(player);
        if (relic == null || _savedData == null)
        {
            return;
        }

        try
        {
            ApertureRunData data = GetState(player);
            relic.RefreshApertureVisualState(data);
        }
        catch (Exception exception)
        {
            Entry.Logger.Info(
                $"刷新空窍遗物显示失败：{exception}"
            );
        }
    }

    private static CardModel? CreateImmortalEssence(
        int rank,
        Player owner
    )
    {
        if (owner.Creature.CombatState is not { } combatState)
        {
            return null;
        }

        CardModel? canonical = rank switch
        {
            6 => ModelDb.Card<QingTiXianYuan>(),
            7 => ModelDb.Card<HongZaoXianYuan>(),
            8 => ModelDb.Card<BaiLiXianYuan>(),
            9 => ModelDb.Card<HuangXingXianYuan>(),
            _ => null,
        };

        /*
         * 战斗卡必须由当前 CombatState 创建。仅 ToMutable 并设置 Owner
         * 虽然能暂时进入手牌，却不会登记进 CombatState.AllCards；余额
         * 归零后 CardCmd.Exhaust 会因此抛出“must be added to a
         * CombatState”。
         */
        return canonical == null
            ? null
            : combatState.CreateCard(canonical, owner);
    }

    private static int GetVictoryXp(CombatRoom room)
    {
        return room.RoomType switch
        {
            RoomType.Boss => 5,
            RoomType.Elite => 3,
            _ => 1,
        };
    }

    private static async Task ResumePendingRankAdvanceAsync(
        Player player
    )
    {
        PlayerRunKey key = new(
            player.RunState,
            player.NetId
        );
        SemaphoreSlim gate = RankAdvanceLocks.GetOrAdd(
            key,
            static _ => new SemaphoreSlim(1, 1)
        );

        await gate.WaitAsync();
        try
        {
            ApertureRunData state = GetState(player);
            int previousRank = state.PendingRankAdvanceFrom;
            int targetRank = state.PendingRankAdvanceTo;

            if (previousRank <= 0 || targetRank <= previousRank)
            {
                return;
            }

            // 蛊阶提升是幂等的：只补到目标转数。
            AutoUpgradeVitalGu(player, targetRank);

            await ApplyPendingMaxHpAwardsAsync(
                player,
                targetRank
            );

            bool shouldNotify = false;
            _savedData!.Modify(
                player,
                data =>
                {
                    data.Normalize();
                    if (data.RankAdvanceNotifiedThroughRank >= targetRank)
                    {
                        return;
                    }

                    // 先提交“已通知”再调用外部扩展，保证重连时至多执行一次。
                    data.RankAdvanceNotifiedThroughRank = targetRank;
                    shouldNotify = true;
                }
            );

            if (shouldNotify)
            {
                InvokeRankAdvanceExtensions(
                    player,
                    previousRank,
                    targetRank
                );
            }

            _savedData!.Modify(
                player,
                data =>
                {
                    data.Normalize();
                    if (data.PendingRankAdvanceFrom == previousRank &&
                        data.PendingRankAdvanceTo == targetRank)
                    {
                        data.PendingRankAdvanceFrom = 0;
                        data.PendingRankAdvanceTo = 0;
                    }
                }
            );

            Entry.Logger.Info(
                $"玩家 {player.NetId} 空窍突破：" +
                $"{previousRank} 转 -> {targetRank} 转。"
            );
        }
        finally
        {
            gate.Release();
        }
    }

    private static async Task ApplyPendingMaxHpAwardsAsync(
        Player player,
        int targetRank
    )
    {
        while (true)
        {
            ApertureRunData state = GetState(player);

            if (state.MaxHpAwardInProgressRank > 0)
            {
                int awardRank = state.MaxHpAwardInProgressRank;
                int expectedMaxHp = checked(
                    state.MaxHpBeforePendingAward +
                    ApertureProgression.GetMaxHpAward(awardRank)
                );
                decimal missingMaxHp = Math.Max(
                    0m,
                    expectedMaxHp - player.Creature.MaxHp
                );

                if (missingMaxHp > 0m)
                {
                    await CreatureCmd.GainMaxHp(
                        player.Creature,
                        missingMaxHp
                    );
                }

                _savedData!.Modify(
                    player,
                    data =>
                    {
                        data.Normalize();
                        if (data.MaxHpAwardInProgressRank != awardRank)
                        {
                            return;
                        }

                        data.MaxHpAppliedThroughRank = Math.Max(
                            data.MaxHpAppliedThroughRank,
                            awardRank
                        );
                        data.MaxHpAwardInProgressRank = 0;
                        data.MaxHpBeforePendingAward = 0;
                    }
                );

                continue;
            }

            int nextRank = Math.Max(
                ApertureProgression.ImmortalRank,
                state.MaxHpAppliedThroughRank + 1
            );

            if (nextRank > targetRank)
            {
                return;
            }

            int maxHpBeforeAward = checked(
                (int)player.Creature.MaxHp
            );
            _savedData!.Modify(
                player,
                data =>
                {
                    data.Normalize();
                    if (data.MaxHpAwardInProgressRank != 0 ||
                        data.MaxHpAppliedThroughRank >= nextRank)
                    {
                        return;
                    }

                    data.MaxHpAwardInProgressRank = nextRank;
                    data.MaxHpBeforePendingAward = maxHpBeforeAward;
                }
            );
        }
    }

    private static void InvokeRankAdvanceExtensions(
        Player player,
        int previousRank,
        int currentRank
    )
    {
        try
        {
            ApertureContentBridge.RankAdvanced?.Invoke(
                player,
                previousRank,
                currentRank
            );
        }
        catch (Exception exception)
        {
            Entry.Logger.Info(
                $"空窍升转扩展回调失败：{exception}"
            );
        }

        if (currentRank != ApertureProgression.MaximumImplementedRank)
        {
            return;
        }

        bool hasShaGu = player.Deck.Cards.Any(card =>
            string.Equals(
                card.GetType().Name,
                "ShaGu",
                StringComparison.Ordinal
            )
        );

        try
        {
            ApertureContentBridge.PlayRankNineTheme?.Invoke(
                player,
                hasShaGu
            );
        }
        catch (Exception exception)
        {
            Entry.Logger.Info(
                $"九转主题扩展回调失败：{exception}"
            );
        }
    }

    private static void AutoUpgradeVitalGu(
        Player player,
        int targetRank
    )
    {
        foreach (CardModel card in player.Deck.Cards)
        {
            if (!card.Tags.Contains(GuZhenRenTags.BenMingGu))
            {
                continue;
            }

            switch (card)
            {
                case AbstractBenMingGuCard vitalGu:
                    while (vitalGu.GuRank < targetRank &&
                           vitalGu.TryIncreaseGuRank())
                    {
                    }
                    break;

                case AbstractGuZhenRenCard guCard
                    when guCard is IGuWormCard:
                    while (guCard.GuRank < targetRank &&
                           guCard.TryIncreaseGuRank())
                    {
                    }
                    break;
            }
        }
    }

    private static bool HasAperture(Player player)
    {
        return FindApertureRelic(player) != null;
    }

    private static KongQiaoRelic? FindApertureRelic(Player player)
    {
        return player.Relics
            .OfType<KongQiaoRelic>()
            .FirstOrDefault();
    }

    private static void EnsureAvailable()
    {
        if (!_initialized || _savedData == null)
        {
            throw new InvalidOperationException(
                "空窍/仙窍运行时尚未初始化。"
            );
        }
    }

    private readonly record struct PlayerRunKey(
        IRunState RunState,
        ulong PlayerNetId
    );
}
