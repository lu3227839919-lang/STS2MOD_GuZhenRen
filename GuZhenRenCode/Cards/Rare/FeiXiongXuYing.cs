using System;
using System.Collections.Generic;
using System.Threading.Tasks;

// 蛊真人公共卡牌父类。
using GuZhenRen.Cards;

// 蛊真人卡池。
using GuZhenRen.Characters;

// RitsuLib 卡牌自动注册特性。
using STS2RitsuLib.Interop.AutoRegistration;

// 卡牌类型与出牌信息。
using MegaCrit.Sts2.Core.Entities.Cards;

// 生物目标。
using MegaCrit.Sts2.Core.Entities.Creatures;

// 异步玩家选择上下文。
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

// 本地化与伤害动态变量。
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

// 基础模型与原生力量能力。
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

// 伤害属性及 IsPoweredAttack 扩展。
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Utils;

namespace GuZhenRen.Cards.Rare;

/// <summary>
/// 飞熊虚影。
///
/// 当玩家打出一张非虚影攻击牌时，本牌有 25% 概率自动触发：
///
/// - 对所有敌人造成 5 点基础伤害；
/// - 力量对本次伤害按 2 倍计算；
/// - 升级后，力量按 3 倍计算。
///
/// 概率触发、保留、不能被打出、虚影属性、目标检查和防递归，
/// 均由 AbstractXuYingCard 统一处理。
/// </summary>
[RegisterCard(typeof(GuZhenRenXuYingCardPool))]
public sealed class FeiXiongXuYing
    : AbstractXuYingCard
{


    /// <summary>
    /// 当前卡牌使用的卡图资源。
    /// </summary>
    public override CardAssetProfile AssetProfile => new(
        PortraitPath:
            $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    // =====================================================================
    //  基础数值
    // =====================================================================

    /// <summary>
    /// 每个敌人受到的基础伤害。
    /// </summary>
    private const decimal BaseDamage = 5m;

    /// <summary>
    /// 基础触发概率：25%。
    /// </summary>
    private const float TriggerChance = 0.25f;

    /// <summary>
    /// 基础力量倍率：2 倍。
    /// </summary>
    private const int BaseStrengthMultiplier = 2;

    /// <summary>
    /// 升级后力量倍率增加 1，即 2 倍变为 3 倍。
    /// </summary>
    private const int UpgradeStrengthMultiplier = 1;

    private static readonly SavedAttachedState<CardModel, int>
        StrengthMultiplierState =
            new(
                "gu_zhen_ren.card.fei_xiong_xu_ying.strength_multiplier",
                static () => BaseStrengthMultiplier
            );

    // =====================================================================
    //  构造函数
    // =====================================================================

    /// <summary>
    /// 创建飞熊虚影。
    /// </summary>
    public FeiXiongXuYing()
        : base(
            // 尖塔1使用 -2 表示不能手动使用。
            //
            // 尖塔2由 AbstractXuYingCard.IsPlayable=false 禁止手动使用。
            // 负费用同时让原版 NCard 隐藏普通能量图标。
            baseCost: -2,

            type: CardType.Attack,
            target: TargetType.AllEnemies
        )
    {
        // 设置 25% 基础触发概率。
        SetBaseChance(
            TriggerChance
        );

        // 初始力量倍率为 2。
        StrengthMultiplier =
            BaseStrengthMultiplier;
    }

    // =====================================================================
    //  动态变量与描述
    // =====================================================================

    /// <summary>
    /// 当前力量倍率。
    ///
    /// 未升级：2。
    /// 升级后：3。
    /// </summary>
    public int StrengthMultiplier
    {
        get => StrengthMultiplierState[this];
        private set => StrengthMultiplierState[this] = value;
    }

    /// <summary>
    /// 飞熊虚影使用一个基础伤害动态变量。
    ///
    /// 实际伤害仍会经过：
    ///
    /// - 力量；
    /// - 易伤；
    /// - 活力；
    /// - 其他伤害 Hook。
    ///
    /// 额外的力量倍率由 ModifyDamageAdditive 补充。
    /// </summary>
    protected override IEnumerable<DynamicVar>
        CanonicalVars =>
        [
            new DamageVar(
                BaseDamage,
                ValueProp.Move
            )
        ];

    /// <summary>
    /// 给卡牌描述补充参数。
    ///
    /// 本地化文本可以使用：
    ///
    /// - {ChancePercent}
    /// - {StrengthMultiplier}
    /// - {Rank}
    /// </summary>
    protected override void AddExtraArgsToDescription(
        LocString description
    )
    {
        base.AddExtraArgsToDescription(
            description
        );

        description.Add(
            "ChancePercent",
            (int)MathF.Round(
                GetEffectiveChance() *
                100f
            )
        );

        description.Add(
            "StrengthMultiplier",
            StrengthMultiplier
        );

    }

    // =====================================================================
    //  力量倍率
    // =====================================================================

    /// <summary>
    /// 给当前飞熊虚影的攻击补充额外力量伤害。
    ///
    /// 原生 StrengthPower 已经会增加 1 倍力量。
    /// 本方法只补充剩余部分：
    ///
    /// 额外伤害 = 力量 × (力量倍率 - 1)
    ///
    /// 因此：
    ///
    /// - 2 倍力量：原生 1 倍 + 此处额外 1 倍；
    /// - 3 倍力量：原生 1 倍 + 此处额外 2 倍。
    ///
    /// 只在 cardSource 正是当前飞熊虚影时生效，
    /// 不会修改玩家打出的其他攻击牌。
    /// </summary>
    public override decimal ModifyDamageAdditive(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay
    )
    {
        // 只修改由当前虚影自身产生的攻击。
        if (!ReferenceEquals(
                cardSource,
                this
            ))
        {
            return 0m;
        }

        // 攻击者必须是当前虚影所属玩家。
        if (!ReferenceEquals(
                dealer,
                Owner.Creature
            ))
        {
            return 0m;
        }

        // 只修改正常的 powered attack。
        if (!props.IsPoweredAttack())
        {
            return 0m;
        }

        // 没有力量时不增加伤害。
        int strength =
            Owner.Creature
                .GetPower<StrengthPower>()
                ?.Amount ??
            0;

        return strength *
               (StrengthMultiplier - 1);
    }

    // =====================================================================
    //  虚影效果
    // =====================================================================

    /// <summary>
    /// 概率判定成功后，对所有敌人执行一次群体攻击。
    ///
    /// 当前虚影不使用单体 target 参数，
    /// 因为原 Java 卡牌效果是群体伤害。
    /// </summary>
    protected override async Task TriggerPhantomEffect(
        PlayerChoiceContext choiceContext,
        CardPlay triggeringPlay,
        Creature? target
    )
    {
        var combatState =
            CombatState;

        if (combatState == null)
        {
            return;
        }

        // CreatePhantomAttack 会执行 FromCard(this)，确保：
        //
        // - 飞熊虚影自身成为伤害来源；
        // - 原生力量、活力、易伤等 Hook 正常工作；
        // - 不会继承触发攻击牌的钢笔尖双倍。
        await CreatePhantomAttack(
                DynamicVars.Damage.BaseValue
            )
            .TargetingAllOpponents(
                combatState
            )

            // 虚影后续会使用自己的卡牌动画，
            // 这里不播放玩家角色的普通攻击动作。
            .WithNoAttackerAnim()
            .Execute(
                choiceContext
            );
    }

    // =====================================================================
    //  升级
    // =====================================================================

    /// <summary>
    /// 普通升级飞熊虚影。
    ///
    /// 基础伤害、概率和转数不变，力量倍率从 2 提高到 3。
    /// 转数成长由 AbstractXuYingCard 的独立转数生命周期处理。
    /// </summary>
    protected override void OnXuYingNormalUpgrade()
    {
        StrengthMultiplier +=
            UpgradeStrengthMultiplier;
    }
}
