using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

// 蛊真人卡牌公共父类所在命名空间。
using GuZhenRen.Cards;
using GuZhenRen.Characters;
using GuZhenRen.Multiplayer;

// 战斗状态接口。
using MegaCrit.Sts2.Core.Combat;

// DamageCmd 等游戏命令。
using MegaCrit.Sts2.Core.Commands;

// AttackCommand 攻击构建器。
using MegaCrit.Sts2.Core.Commands.Builders;

// 卡牌、卡牌关键词、出牌记录和牌堆类型。
using MegaCrit.Sts2.Core.Entities.Cards;

// 生物目标。
using MegaCrit.Sts2.Core.Entities.Creatures;

// 可能产生玩家选择的异步上下文。
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

// 本地化文本。
using MegaCrit.Sts2.Core.Localization;

// CardModel 等游戏模型。
using MegaCrit.Sts2.Core.Models;

// NextItem 等确定性随机扩展。
using MegaCrit.Sts2.Core.Random;

using STS2RitsuLib;
using STS2RitsuLib.Utils;

namespace GuZhenRen.Cards;

/// <summary>
/// 所有“虚影”卡牌的公共父类。
///
/// 尖塔1虚影的共同规则：
///
/// 1. 默认属于力道；
/// 2. 回合结束时仍会自动保留，但不显示“保留”关键词；
/// 3. 玩家仍不能手动使用，但不显示“不能被打出”关键词；
/// 4. 玩家打出一张非虚影攻击牌后，按概率自动触发；
/// 5. 虚影复制牌不会再次触发虚影；
/// 6. 抽到虚影时重置执行状态；
/// 7. 原目标已经死亡时，重新选择随机有效敌人；
/// 8. 同一张虚影尚未执行完成时不会重入。
///
/// 本版本已经直接连接尖塔2的 CardModel 生命周期：
///
/// - CanonicalKeywords 仅公开“虚影”；
/// - 隐藏的保留和不可手动使用规则；
/// - AfterCardDrawn；
/// - AfterCardPlayed；
/// - AfterCloned。
///
/// 钢笔尖已经确认不需要额外伤害修正：
/// 只要具体虚影造成伤害时把 cardSource 设为虚影自身，
/// 就不会继承触发攻击牌的钢笔尖双倍。
///
/// 活力已经通过原生 AttackCommand 生命周期接入：
/// 攻击型虚影必须使用 FromCard(this) 构建攻击，
/// VigorPower 会自动加伤并在攻击结束后消耗。
///
/// 虚影显化时通过原生手牌→出牌区→手牌流程播放动画；
/// 青色发光仍保留为后续扩展点。
/// </summary>
public abstract class AbstractXuYingCard
    : AbstractGuZhenRenCard,
      IProbabilityCard
{
    /// <summary>
    /// 虚影使用独立隐藏卡池，避免落入普通卡池或 MockCardPool 回退。
    /// </summary>
    public override CardPoolModel Pool =>
        ModelDb.CardPool<GuZhenRenXuYingCardPool>();

    /// <summary>
    /// 概率判定使用的整数精度。
    ///
    /// 1,000,000 表示概率精确到百万分之一。
    /// 使用游戏跑局 RNG 的 NextInt，避免 Random.Shared
    /// 对回放和多人同步造成不确定性。
    /// </summary>
    private const int ProbabilityScale = 1_000_000;

    /// <summary>
    /// 普通实例字段会被 CardModel.MemberwiseClone 一并复制。
    ///
    /// SavedAttachedState 负责存档，但不会随 ToMutable/MutableClone 自动
    /// 复制到新实例。只使用附加状态会导致规范模型为 25%，而战斗生成的
    /// 可变飞熊虚影回落为 0%。
    /// </summary>
    private int _baseChanceScaled;

    private decimal _resolutionMultiplier = 1m;

    private static readonly SavedAttachedState<CardModel, int>
        BaseChanceState =
            new(
                "lu_gu_zhen_ren.card.xu_ying.base_chance_scaled",
                static () => 0
            );

    /// <summary>
    /// 区分“存档中明确保存了 0%”和“新克隆尚无附加状态”。
    ///
    /// 新克隆优先使用随 MemberwiseClone 复制的普通字段；
    /// 读档后的实例优先使用附加保存状态。
    /// </summary>
    private static readonly SavedAttachedState<CardModel, bool>
        BaseChanceStateInitialized =
            new(
                "lu_gu_zhen_ren.card.xu_ying.base_chance_initialized",
                static () => false
            );

    /// <summary>
    /// 描述文本中的概率占位符。
    /// </summary>
    public const string ChancePlaceholder = "{CHANCE}";

    /// <summary>
    /// 创建一张虚影。
    /// </summary>
    /// <param name="baseCost">
    /// 卡牌基础费用。
    ///
    /// 虚影不能手动使用，但仍保留费用供 UI 和其他效果读取。
    /// </param>
    /// <param name="type">
    /// 卡牌类型。
    /// </param>
    /// <param name="target">
    /// 虚影效果的目标类型。
    /// </param>
    /// <param name="showInCardLibrary">
    /// 是否在图鉴中显示。
    /// </param>
    protected AbstractXuYingCard(
        int baseCost,
        CardType type,
        TargetType target,
        bool showInCardLibrary = true
    )
        : base(
            baseCost,
            type,

            // 尖塔2没有 CardRarity.Special。
            // 使用 Rare 保留特殊卡原本使用的稀有卡框。
            CardRarity.Rare,

            target,
            showInCardLibrary
        )
    {
        // 所有虚影默认属于力道。
        SetDao(Dao.LiDao);
    }

    // =====================================================================
    //  虚影转数与普通升级
    // =====================================================================

    /// <summary>
    /// 让虚影同步来源蛊牌的两套独立成长状态：
    ///
    /// 1. 蛊虫转数；
    /// 2. 游戏原生普通升级。
    ///
    /// 所有生成虚影和预览虚影都应调用该入口，避免只同步其中一项。
    /// </summary>
    internal void SynchronizeProgressionFrom(
        AbstractGuZhenRenCard sourceCard
    )
    {
        ArgumentNullException.ThrowIfNull(
            sourceCard
        );

        // 转数同步会触发父类统一的读取/变化钩子，
        // 让具体虚影刷新依赖转数的伤害、概率或其他数值。
        ReconcileGuRankForUniqueness(
            sourceCard.GuRank
        );

        // 普通升级与转数是相互独立的成长系统。
        // 来源牌已升级时，生成的虚影也执行一次标准升级生命周期。
        if (sourceCard.IsUpgraded &&
            !IsUpgraded &&
            IsUpgradable)
        {
            UpgradeInternal();
            FinalizeUpgradeInternal();
        }
    }

    /// <summary>
    /// 虚影的游戏原生普通升级统一入口。
    ///
    /// 子类不要再重写 OnUpgrade；应重写 OnXuYingNormalUpgrade，
    /// 从而确保所有虚影都经过相同的父类升级生命周期。
    /// </summary>
    protected sealed override void OnUpgrade()
    {
        base.OnUpgrade();
        OnXuYingNormalUpgrade();
    }

    /// <summary>
    /// 具体虚影的普通升级效果。
    /// </summary>
    protected virtual void OnXuYingNormalUpgrade()
    {
    }

    /// <summary>
    /// 虚影成功升转后的统一入口。
    /// </summary>
    protected sealed override void OnGuRankIncreased(
        int previousGuRank,
        int newGuRank
    )
    {
        base.OnGuRankIncreased(
            previousGuRank,
            newGuRank
        );

        OnXuYingGuRankIncreased(
            previousGuRank,
            newGuRank
        );
    }

    /// <summary>
    /// 具体虚影在主动升转成功后执行的效果。
    /// </summary>
    protected virtual void OnXuYingGuRankIncreased(
        int previousGuRank,
        int newGuRank
    )
    {
    }

    /// <summary>
    /// 转数发生任何变化后的统一刷新入口，包括生成同步、读档、
    /// 复制和主动升转。
    /// </summary>
    protected sealed override void OnGuRankChanged()
    {
        base.OnGuRankChanged();
        OnXuYingGuRankChanged();
    }

    /// <summary>
    /// 具体虚影刷新依赖当前转数的派生数值。
    /// </summary>
    protected virtual void OnXuYingGuRankChanged()
    {
    }

    /// <summary>
    /// 读档或克隆恢复转数后的统一入口。
    /// </summary>
    protected sealed override void OnGuRankLoaded()
    {
        base.OnGuRankLoaded();
        OnXuYingGuRankLoaded();
    }

    /// <summary>
    /// 具体虚影在读档或克隆恢复后重建临时派生状态。
    /// </summary>
    protected virtual void OnXuYingGuRankLoaded()
    {
    }

    // =====================================================================
    //  可见标签与隐藏规则
    // =====================================================================

    /// <summary>
    /// 虚影卡牌公开显示的关键词只有“虚影”。
    ///
    /// 转数与流派由蛊虫牌公共标签系统显示；“保留”和
    /// “不能被打出”仍作为隐藏机制生效，不再占用卡面标签。
    /// </summary>
    public override IEnumerable<CardKeyword>
        CanonicalKeywords =>
        [
            GuZhenRenKeywords.XuYing,
            CardKeyword.Retain,
        ];

    /// <summary>
    /// 不依赖 Unplayable 关键词，直接通过原版卡牌逻辑阻止玩家
    /// 手动使用虚影，因此卡面不会显示“不能被打出”标签。
    /// </summary>
    protected sealed override bool IsPlayable =>
        false;

    // =====================================================================
    //  概率
    // =====================================================================

    /// <summary>
    /// 基础触发概率，范围为 0～1。
    /// </summary>
    public float BaseChance
    {
        get
        {
            int scaledChance =
                BaseChanceStateInitialized[this]
                    ? BaseChanceState[this]
                    : _baseChanceScaled;

            return scaledChance /
                (float)ProbabilityScale;
        }
    }

    /// <summary>
    /// 增加或减少基础概率。
    ///
    /// 最终结果始终限制在 0～1。
    /// </summary>
    public void IncreaseBaseChance(float amount)
    {
        SetBaseChance(
            BaseChance + amount
        );
    }

    /// <summary>
    /// 设置基础触发概率。
    ///
    /// 具体虚影通常在构造函数中调用。
    /// </summary>
    protected void SetBaseChance(float chance)
    {
        float clampedChance =
            Math.Clamp(
                chance,
                0.0f,
                1.0f
            );

        int scaledChance =
            (int)MathF.Round(
                clampedChance *
                ProbabilityScale
            );

        // 普通字段确保 ToMutable/MutableClone 后仍保留概率。
        _baseChanceScaled = scaledChance;

        // 附加状态确保保存、读档和多人快照仍能恢复概率。
        BaseChanceState[this] = scaledChance;
        BaseChanceStateInitialized[this] = true;

        OnBaseChanceChanged();
    }

    /// <summary>
    /// 基础概率变化后的扩展钩子。
    ///
    /// 后续可用于刷新动态变量和描述。
    /// </summary>
    protected virtual void OnBaseChanceChanged()
    {
    }

    protected decimal ResolutionMultiplier => _resolutionMultiplier;

    protected virtual bool UsesCentralResolution => false;

    /// <summary>
    /// 取得包括能力修正在内的最终触发概率。
    ///
    /// 最终结果始终限制在 0～1。
    /// </summary>
    public float GetEffectiveChance()
    {
        float chance =
            BaseChance;

        // 图鉴中的规范模型没有 Owner，
        // 此时只显示和使用基础概率。
        if (!IsMutable)
        {
            return chance;
        }

        if (Owner == null)
        {
            return chance;
        }

        foreach (IProbabilityModifier modifier
                 in Owner.Creature
                    .Powers
                    .OfType<IProbabilityModifier>())
        {
            chance +=
                modifier
                    .GetAdditiveProbability(
                        this
                    );
        }

        return Math.Clamp(
            chance,
            0.0f,
            1.0f
        );
    }

    /// <summary>
    /// 使用跑局的确定性 RNG 进行概率判定。
    ///
    /// 使用按模组、玩家和机制隔离的 RitsuLib RNG 流，
    /// 不推进游戏本体的随机序列。
    /// </summary>
    private bool RollProbability()
    {
        float effectiveChance =
            GetEffectiveChance();

        if (effectiveChance <= 0.0f)
        {
            return false;
        }

        if (effectiveChance >= 1.0f)
        {
            return true;
        }

        int roll = RitsuLibFramework
            .GetModPlayerRng(
                Owner,
                Entry.ModId,
                "xu_ying/probability"
            )
            .NextInt(ProbabilityScale);

        int threshold =
            (int)MathF.Floor(
                effectiveChance *
                ProbabilityScale
            );

        return roll < threshold;
    }

    // =====================================================================
    //  执行状态
    // =====================================================================

    /// <summary>
    /// 当前虚影是否正在执行自动效果。
    ///
    /// 该标记同时用于：
///
/// - 防止递归重入；
/// - 后续控制青色发光；
/// - 防止虚影效果触发的嵌套出牌再次触发同一张虚影。
    /// </summary>
    private int _phantomExecutionGate;

    public bool IsPhantomExecuting =>
        Volatile.Read(ref _phantomExecutionGate) != 0;

    /// <summary>
    /// 抽到这张虚影时重置瞬时执行状态。
    /// </summary>
    private void ResetExecutionState()
    {
        Interlocked.Exchange(
            ref _phantomExecutionGate,
            0
        );

        OnPhantomExecutionStateChanged(
            isExecuting: false
        );
    }

    /// <summary>
    /// 虚影执行状态发生变化后的 UI 扩展钩子。
    ///
    /// 后续可以在这里开始或停止青色发光。
    /// </summary>
    protected virtual void OnPhantomExecutionStateChanged(
        bool isExecuting
    )
    {
    }

    // =====================================================================
    //  抽牌生命周期
    // =====================================================================

    /// <summary>
    /// 每次抽牌后由游戏 Hook 调用。
    ///
    /// 只有被抽到的卡牌正是当前虚影实例时才重置状态。
    /// </summary>
    public override Task AfterCardDrawn(
        PlayerChoiceContext choiceContext,
        CardModel card,
        bool fromHandDraw
    )
    {
        if (ReferenceEquals(card, this))
        {
            ResetExecutionState();
        }

        return Task.CompletedTask;
    }

    // =====================================================================
    //  出牌后自动触发
    // =====================================================================

    /// <summary>
    /// 其他卡牌完成一次出牌后由游戏 Hook 调用。
    ///
    /// 当前虚影只有满足以下条件才进行概率判定：
///
/// 1. 当前虚影仍在自己的手牌中；
/// 2. 触发牌属于同一个玩家；
/// 3. 触发牌是攻击牌；
/// 4. 触发牌不具有“虚影”属性；
/// 5. 触发牌不带 XuYingCopy 标签；
/// 6. 当前是 Replay 系列中的第一次执行；
/// 7. 本虚影当前没有在执行。
    /// </summary>
    public override async Task AfterCardPlayed(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        ArgumentNullException.ThrowIfNull(
            choiceContext
        );
        ArgumentNullException.ThrowIfNull(
            cardPlay
        );

        if (!CanReactToPlayedCard(cardPlay))
        {
            return;
        }

        // CanReactToPlayedCard 的普通布尔检查不足以阻止两个异步回调
        // 同时通过。这里必须在消费概率 RNG 前原子占用执行权。
        if (Interlocked.CompareExchange(
                ref _phantomExecutionGate,
                1,
                0
            ) != 0)
        {
            return;
        }

        bool executionAnnounced = false;

        try
        {
            if (!RollProbability())
            {
                return;
            }

            Creature? target =
                ResolvePhantomTarget(
                    cardPlay.Target
                );

            executionAnnounced = true;
            OnPhantomExecutionStateChanged(
                isExecuting: true
            );

            await ExecutePhantomAndReturnToHandAsync(
                choiceContext,
                cardPlay,
                target
            );
        }
        finally
        {
            Interlocked.Exchange(
                ref _phantomExecutionGate,
                0
            );

            if (executionAnnounced)
            {
                OnPhantomExecutionStateChanged(
                    isExecuting: false
                );
            }
        }
    }

    /// <summary>
    /// 执行虚影效果，并在结算后保证虚影仍在手牌中。
    ///
    /// 不再在结算前调用 RemoveFromCombat：该命令会让卡牌
    /// 离开战斗状态，之后的回手命令失败时，游戏内会表现为
    /// 虚影显化后被消耗。
    ///
    /// 虚影在效果结算期间保持战斗注册；无论效果正常完成
    /// 还是抛出异常，finally 都使用带校验与内部回退的移牌
    /// 流程将其恢复到手牌。概率未触发时不进入此流程。
    /// </summary>
    private async Task ExecutePhantomAndReturnToHandAsync(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        Creature? target
    )
    {
        try
        {
            ShowPhantomManifestationNotice();
            await MovePhantomToPlayPileAsync();

            await TriggerPhantomEffect(
                choiceContext,
                cardPlay,
                target
            );
        }
        finally
        {
            await GuCardPileSystem.MoveCardToPileAsync(
                this,
                PileType.Hand,
                skipVisuals: false
            );
        }
    }

    /// <summary>
    /// 将虚影与当前触发牌一同放入出牌区。
    ///
    /// AddDuringManualCardPlay 会立即更新牌堆状态，但不等待
    /// 非能力牌的入场 Tween；多张虚影连续显化时，不会在
    /// 加速模式下因逐张等待动画而出现额外停顿。
    ///
    /// 动画初始化失败不得阻止虚影效果；finally 仍会
    /// 将虚影校正回手牌。
    /// </summary>
    private async Task MovePhantomToPlayPileAsync()
    {
        try
        {
            await CardPileCmd.AddDuringManualCardPlay(this);
        }
        catch (Exception exception)
        {
            Entry.Logger.Warn(
                $"[虚影] 出牌动画初始化失败，继续结算：" +
                $"card={Id}, error={exception.Message}"
            );
        }
    }

    /// <summary>
    /// 显示不阻塞结算的虚影显化提示。
    /// </summary>
    private void ShowPhantomManifestationNotice()
    {
        LocString notice = new(
            "cards",
            "GU_ZHEN_REN_PERSONAL_CARD_XU_YING_MANIFESTATION.combatMessage"
        );
        notice.Add(
            "Phantom",
            TitleLocString.GetFormattedText()
        );
        ThinkCmd.Play(
            notice,
            Owner.Creature,
            secondsToDisplay: 1.5d
        );
    }

    internal async Task<bool> TriggerFromControllerAsync(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        bool forced,
        decimal effectMultiplier
    )
    {
        ArgumentNullException.ThrowIfNull(choiceContext);
        ArgumentNullException.ThrowIfNull(cardPlay);

        if (!CanReactToPlayedCard(cardPlay, allowCentralResolution: true) ||
            Interlocked.CompareExchange(ref _phantomExecutionGate, 1, 0) != 0)
        {
            return false;
        }

        bool executionAnnounced = false;
        try
        {
            if (!forced && !RollProbability())
            {
                return false;
            }

            _resolutionMultiplier = Math.Max(0m, effectMultiplier);
            Creature? target = ResolvePhantomTarget(cardPlay.Target);
            executionAnnounced = true;
            OnPhantomExecutionStateChanged(isExecuting: true);
            await ExecutePhantomAndReturnToHandAsync(
                choiceContext,
                cardPlay,
                target
            );
            return true;
        }
        finally
        {
            _resolutionMultiplier = 1m;
            Interlocked.Exchange(ref _phantomExecutionGate, 0);
            if (executionAnnounced)
            {
                OnPhantomExecutionStateChanged(isExecuting: false);
            }
        }
    }

    /// <summary>
    /// 判断一次出牌是否满足虚影的基础触发条件。
    /// </summary>
    private bool CanReactToPlayedCard(
        CardPlay cardPlay,
        bool allowCentralResolution = false
    )
    {
        if (UsesCentralResolution && !allowCentralResolution)
        {
            return false;
        }

        // 同一张虚影尚未执行完成时不能再次进入。
        if (IsPhantomExecuting)
        {
            return false;
        }

        // 虚影必须仍在自己的手牌中。
        if (Pile?.Type != PileType.Hand)
        {
            return false;
        }

        CardModel playedCard =
            cardPlay.Card;

        // 多人游戏中只响应自己所属玩家打出的牌。
        if (!ReferenceEquals(
                playedCard.Owner,
                Owner
            ))
        {
            return false;
        }

        // 只响应攻击牌。
        if (playedCard.Type != CardType.Attack)
        {
            return false;
        }

        // 拥有“虚影”属性的卡牌不会触发其他虚影。
        //
        // 使用关键词判断，而不是依赖具体 C# 继承类型；
        // 以后其他特殊卡只要加入 XuYing 关键词，
        // 同样会被虚影系统正确识别。
        if (playedCard.Keywords.Contains(
                GuZhenRenKeywords.XuYing
            ))
        {
            return false;
        }

        // 虚影效果产生的复制牌不能再次触发，
        // 避免形成递归链。
        if (playedCard.Tags.Contains(
                GuZhenRenTags.XuYingCopy
            ))
        {
            return false;
        }

        // 尖塔2的 Replay 会为同一张牌生成多个 CardPlay。
        // 尖塔1的 onPlayCard 更接近“玩家打出一次牌”，
        // 因此只在系列第一次执行时判定。
        if (!cardPlay.IsFirstInSeries)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 确定本次虚影使用的最终目标。
    ///
    /// 原目标仍是存活敌人时继续使用；
    /// 否则从当前可命中的敌人中随机选择。
    /// </summary>
    private Creature? ResolvePhantomTarget(
        Creature? originalTarget
    )
    {
        if (originalTarget is
            {
                IsAlive: true,
                IsEnemy: true
            })
        {
            return originalTarget;
        }

        ICombatState? combatState =
            CombatState ??
            Owner.Creature.CombatState;

        if (combatState == null)
        {
            return null;
        }

        Creature[] hittableEnemies =
            GuZhenRenDeterminism.OrderCreatures(
                combatState.HittableEnemies
            );

        if (hittableEnemies.Length == 0)
        {
            return null;
        }

        return RitsuLibFramework
            .GetModPlayerRng(
                Owner,
                Entry.ModId,
                "xu_ying/target"
            )
            .NextItem(hittableEnemies);
    }

    /// <summary>
    /// 执行具体虚影效果。
    ///
    /// 每张虚影子类必须实现。
    /// </summary>
    /// <param name="choiceContext">
    /// 当前出牌 Hook 的玩家选择上下文。
    /// </param>
    /// <param name="triggeringPlay">
    /// 触发本虚影的原始 CardPlay。
    /// </param>
    /// <param name="target">
    /// 已验证或重新选择后的目标。
    ///
    /// 战斗已经没有有效敌人时可能为 null。
    /// </param>
    protected abstract Task TriggerPhantomEffect(
        PlayerChoiceContext choiceContext,
        CardPlay triggeringPlay,
        Creature? target
    );

    // =====================================================================
    //  虚影攻击、活力与钢笔尖
    // =====================================================================

    /// <summary>
    /// 创建一条以当前虚影为来源的攻击命令。
    ///
    /// 所有攻击型虚影都应通过该方法构建真实攻击，原因有三：
    ///
    /// 1. `FromCard(this, null)` 会把攻击者设置为虚影所属玩家；
    /// 2. `ModelSource` 会被设置为当前虚影，而不是触发牌；
    /// 3. 原生 VigorPower 会在 BeforeAttack 中为本次攻击加伤，
    ///    并在 AfterAttack 中自动消耗全部参与本次攻击的活力。
    ///
    /// 同时，由于钢笔尖只加倍它记录的那张 `AttackToDouble`，
    /// 当前虚影不会错误继承触发攻击牌的钢笔尖双倍。
    ///
    /// 具体虚影可以继续链式配置：
    ///
    /// CreatePhantomAttack(damage)
    ///     .Targeting(target)
    ///     .WithHitFx("...")
    ///     .WithNoAttackerAnim()
    ///     .Execute(choiceContext);
    /// </summary>
    /// <param name="damagePerHit">
    /// 每次命中的基础伤害。
    /// </param>
    /// <returns>
    /// 已经完成 `FromCard(this, null)` 配置的 AttackCommand。
    /// </returns>
    protected AttackCommand CreatePhantomAttack(
        decimal damagePerHit
    )
    {
        if (Type != CardType.Attack)
        {
            throw new InvalidOperationException(
                "只有攻击型虚影可以使用 " +
                "CreatePhantomAttack。"
            );
        }

        return DamageCmd
            .Attack(damagePerHit)
            .FromCard(this, cardPlay: null);
    }

    // =====================================================================
    //  描述
    // =====================================================================

    /// <summary>
    /// 替换描述中的 {CHANCE} 概率占位符。
    ///
    /// chanceFormatter 负责本地化百分比和颜色格式。
    /// </summary>
    public string BuildChanceDescription(
        string baseDescription,
        Func<float, string> chanceFormatter
    )
    {
        ArgumentNullException.ThrowIfNull(
            baseDescription
        );
        ArgumentNullException.ThrowIfNull(
            chanceFormatter
        );

        return baseDescription.Replace(
            ChancePlaceholder,
            chanceFormatter(BaseChance),
            StringComparison.Ordinal
        );
    }

    // =====================================================================
    //  克隆
    // =====================================================================

    /// <summary>
    /// 卡牌克隆完成后清除战斗瞬时执行状态。
    ///
    /// _baseChanceScaled 等普通字段会由模型克隆流程复制，
    /// 可保存附加状态则在读档时恢复；正在执行状态不能带到新实例。
    /// </summary>
    protected override void AfterCloned()
    {
        base.AfterCloned();

        Interlocked.Exchange(
            ref _phantomExecutionGate,
            0
        );
        _resolutionMultiplier = 1m;
    }
}
