// 蛊真人公共卡牌父类。
using GuZhenRen.Cards;

// 关联卡牌轮播预览接口。
using GuZhenRen.Cards.Interfaces;

// 蛊真人卡池。
using GuZhenRen.Characters;

// RitsuLib 卡牌自动注册特性。
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Utils;

// 群体伤害与生成临时卡牌命令。
using GuZhenRen.Multiplayer;

using MegaCrit.Sts2.Core.Commands;

// 卡牌类型、关键词、出牌信息和牌堆类型。
using MegaCrit.Sts2.Core.Entities.Cards;

// 生物目标。
using MegaCrit.Sts2.Core.Entities.Creatures;

// 异步玩家选择上下文。
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

// 本地化文本与动态变量。
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

// 卡牌模型数据库。
using MegaCrit.Sts2.Core.Models;

// 原生易伤能力。
using MegaCrit.Sts2.Core.Models.Powers;

// 伤害属性。
using MegaCrit.Sts2.Core.ValueProps;


namespace GuZhenRen.Cards.Rare;

/// <summary>
/// 飞熊之力蛊。
///
/// 效果：
///
/// 1. 对所有敌人造成 12 点伤害；
/// 2. 对所有仍然存活且可命中的敌人施加 2 层易伤；
/// 3. 在手牌中生成一张“飞熊虚影”；
/// 4. 消耗。
///
/// 卡牌升级后：
///
/// 1. 易伤从 2 层提高到 4 层；
/// 2. 生成的飞熊虚影同步升级。
///
/// 蛊虫升转是独立流程，普通升级不会提高转数。
///
/// 合练配方：三张蛊真人打击。材料选择顺序不限。
///
/// </summary>
[RegisterCard(typeof(GuZhenRenCardPool))]

public sealed class FeiXiongZhiLiGu
    : AbstractGuZhenRenCard,
      ICarouselCard
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
    /// 基础费用。
    /// </summary>
    private const int Cost = 2;

    /// <summary>
    /// 群体伤害。
    ///
    /// Java 原版升级时不增加伤害。
    /// </summary>
    private const decimal Damage = 12m;

    /// <summary>
    /// 基础易伤层数。
    /// </summary>
    private const int BaseVulnerable = 2;

    /// <summary>
    /// 升级增加的易伤层数。
    /// </summary>
    private const int UpgradeVulnerable = 2;

    private static readonly SavedAttachedState<CardModel, int>
        VulnerableAmountState =
            new(
                "gu_zhen_ren.card.fei_xiong_zhi_li.vulnerable",
                static () => BaseVulnerable
            );

    // =====================================================================
    //  构造函数
    // =====================================================================

    /// <summary>
    /// 创建飞熊之力蛊。
    /// </summary>
    public FeiXiongZhiLiGu()
        : base(
            baseCost: Cost,
            type: CardType.Attack,
            rarity: CardRarity.Rare,
            target: TargetType.AllEnemies
        )
    {
        // 飞熊之力蛊属于力道。
        SetDao(Dao.LiDao);



        // 当前实际易伤层数。
        VulnerableAmount = BaseVulnerable;
    }

    // =====================================================================
    //  卡牌公共配置
    // =====================================================================

    /// <summary>
    /// 当前施加的易伤层数。
    ///
    /// 升级后从 2 变为 4。
    /// 这是普通实例字段，卡牌克隆时会随模型一起复制；
    /// 读档时游戏重放 OnUpgrade，也会重新得到正确数值。
    /// </summary>
    public int VulnerableAmount
    {
        get => VulnerableAmountState[this];
        private set => VulnerableAmountState[this] = value;
    }

    /// <summary>
    /// 声明卡牌使用的伤害动态变量。
    ///
    /// `DynamicVars.Damage` 会由 CardModel 自动提供，
    /// 并参与力量、易伤、钢笔尖等原生伤害修正。
    /// </summary>
    protected override IEnumerable<DynamicVar>
        CanonicalVars =>
        [
            new DamageVar(
                Damage,
                ValueProp.Move
            )
        ];

    /// <summary>
    /// 飞熊之力蛊使用后消耗。
    /// </summary>
    public override IEnumerable<CardKeyword>
        CanonicalKeywords =>
            base.CanonicalKeywords.Append(
                CardKeyword.Exhaust
            );

    /// <summary>
    /// 给本地化描述补充非 DynamicVar 参数。
    ///
    /// 本地化 JSON 中可以使用：
    ///
    /// {Vulnerable}
    /// {Rank}
    ///
    /// 显示当前易伤层数和品阶。
    /// </summary>
    protected override void AddExtraArgsToDescription(
        LocString description
    )
    {
        base.AddExtraArgsToDescription(
            description
        );

        description.Add(
            "Vulnerable",
            VulnerableAmount
        );

        description.Add(
            "Rank",
            GuRank
        );
    }

    // =====================================================================
    //  出牌效果
    // =====================================================================

    /// <summary>
    /// 使用飞熊之力蛊。
    /// </summary>
    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        var combatState =
            CombatState;

        if (combatState == null)
        {
            return;
        }

        ArgumentNullException.ThrowIfNull(
            choiceContext
        );
        ArgumentNullException.ThrowIfNull(
            cardPlay
        );

        // -------------------------------------------------------------
        // 1. 对所有敌人造成群体伤害。
        // -------------------------------------------------------------
        //
        // FromCard(this) 保证：
        //
        // - 当前卡牌成为真正的伤害来源；
        // - 力量、钢笔尖等原生效果能够正确识别；
        // - 攻击历史和伤害 Hook 正常工作。
        await DamageCmd
            .Attack(
                DynamicVars.Damage.BaseValue
            )
            .FromCard(this, cardPlay)
            .TargetingAllOpponents(
                combatState
            )
            .Execute(
                choiceContext
            );

        // -------------------------------------------------------------
        // 2. 对伤害结算后仍可命中的敌人施加易伤。
        // -------------------------------------------------------------
        //
        // 使用当前的 HittableEnemies，
        // 可以自然排除已经被群体伤害击杀的敌人。
        Creature[] vulnerableTargets =
            GuZhenRenDeterminism.OrderCreatures(
                combatState.HittableEnemies
            );

        if (vulnerableTargets.Length > 0)
        {
            await PowerCmd.Apply<VulnerablePower>(
                choiceContext,
                vulnerableTargets,
                VulnerableAmount,
                Owner.Creature,
                this
            );
        }

        // -------------------------------------------------------------
        // 3. 生成飞熊虚影到手牌。
        // -------------------------------------------------------------
        FeiXiongXuYing xuYing =
            CreateGeneratedXuYing();

        await CardPileCmd.AddGeneratedCardToCombat(
            xuYing,
            PileType.Hand,
            Owner
        );
    }

    // =====================================================================
    //  飞熊虚影生成与预览
    // =====================================================================

    /// <summary>
    /// 创建一张用于战斗的飞熊虚影。
    ///
    /// 使用 ModelDb 中注册的规范卡牌创建 mutable 实例，
    /// 而不是直接 `new FeiXiongXuYing()`。
    /// 这样可以保留 RitsuLib 注册的模型 ID、资源和本地化。
    /// </summary>
    private FeiXiongXuYing CreateGeneratedXuYing()
    {
        FeiXiongXuYing xuYing =
            (FeiXiongXuYing)
            ModelDb
                .Card<FeiXiongXuYing>()
                .ToMutable();

        // AddGeneratedCardToCombat 要求生成牌已经拥有 Owner。
        xuYing.Owner = Owner;

        // 虚影同样属于蛊虫牌并拥有转数。生成时继承当前
        // 飞熊之力蛊的转数，而不是回落到规范模型的一转。
        // 父类统一同步虚影的当前转数和普通升级状态。
        xuYing.SynchronizeProgressionFrom(
            this
        );

        return xuYing;
    }

    /// <summary>
    /// 创建一张用于 UI 预览的飞熊虚影。
    ///
    /// 当前父类还没有接入“关联卡牌预览”UI，
    /// 但该方法已经提供正确的预览模型。
    /// 后续取得 HoverTipFactory 或卡牌预览节点接口后，
    /// 可以直接调用本方法。
    /// </summary>
    public CardModel CreateXuYingPreview()
    {
        FeiXiongXuYing preview =
            (FeiXiongXuYing)
            ModelDb
                .Card<FeiXiongXuYing>()
                .ToMutable();

        preview.SynchronizeProgressionFrom(
            this
        );

        return preview;
    }

    // 虚影的转数与普通升级同步逻辑已统一下沉到
    // AbstractXuYingCard.SynchronizeProgressionFrom。

    // =====================================================================
    //  关联卡牌轮播预览
    // =====================================================================

    /// <summary>
    /// 飞熊之力蛊悬停时轮播展示其生成的飞熊虚影。
    ///
    /// 预览虚影会同步当前主卡的转数和普通升级状态。
    /// </summary>
    public IReadOnlyList<CardModel>
        GetCarouselCards() =>
        [
            CreateXuYingPreview(),
        ];

    public double CarouselIntervalSeconds =>
        2.5d;

    // =====================================================================
    //  升级
    // =====================================================================

    /// <summary>
    /// 升级飞熊之力蛊。
    ///
    /// Java 原版：
    ///
    /// - 易伤 +2；
    /// - 飞熊虚影预览同步升级。
    ///
    /// 尖塔2每次生成或创建预览时都会读取主卡的 IsUpgraded，
    /// 因此不需要长期保存一张 `cardsToPreview` 实例。
    /// </summary>
    protected override void OnUpgrade()
    {
        base.OnUpgrade();

        // 这里只执行游戏原生卡牌升级效果。
        VulnerableAmount +=
            UpgradeVulnerable;
    }

}
