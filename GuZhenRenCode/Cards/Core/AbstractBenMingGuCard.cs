
// 游戏卡牌相关类型。
using MegaCrit.Sts2.Core.Entities.Cards;

// 游戏模型基类和 CardModel。
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Entities.Players;
// RitsuLib 自动注册注解。
using STS2RitsuLib.Interop.AutoRegistration;

// RitsuLib Mod 卡牌模板。
using STS2RitsuLib.Scaffolding.Content;

// RitsuLib 可保存附加状态。
using STS2RitsuLib.Utils;

// 游戏命令，例如 CreatureCmd。
using MegaCrit.Sts2.Core.Commands;

// 无需玩家进行额外选择的命令上下文。
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

// 伤害属性，例如 Unblockable、Unpowered。
using MegaCrit.Sts2.Core.ValueProps;

using GuZhenRen.Characters;
using GuZhenRen.Cards.ShaZhao;
using GuZhenRen.Cards.HeLian;

namespace GuZhenRen.Cards;

/// <summary>
/// 所有本命蛊卡牌的抽象父类。
///
/// 这个类统一负责：
/// 1. 自动添加本命蛊标签；
/// 2. 保存本命蛊当前品阶；
/// 3. 限制最高品阶；
/// 4. 提供统一升转流程；
/// 5. 强制具体卡牌实现自己的升转效果。
///
/// 抽象类本身不会成为一张实际卡牌。
/// </summary>
[RegisterCard(
    typeof(GuZhenRenGuCardPool),
    Inherit = true
)]
public abstract class AbstractBenMingGuCard : ModCardTemplate, IGuWormCard
{
    /// <summary>
    /// 默认使用与具体运行时卡牌类型同名的 PNG。
    /// </summary>
    public override CardAssetProfile AssetProfile =>
        CardImageCatalog.Create(GetType());

    /// <summary>
    /// 本命蛊每次恢复后默认可催动一次。
    /// </summary>
    public virtual int MaxUses => 1;

    protected override bool IsPlayable =>
        GuActivationModeSystem.CanPlay(this);

    public override bool ShouldPlay(
        CardModel card,
        AutoPlayType autoPlayType
    )
    {
        return base.ShouldPlay(card, autoPlayType) &&
               (!ReferenceEquals(card, this) ||
                GuCardUsageRules.CanUse(this));
    }

    /// <summary>
    /// 本命蛊从专属蛊牌堆催动；剩余使用次数归零后进入蛊弃牌堆，
    /// 否则返回可催动的蛊牌堆。
    /// </summary>
    public override CardLocation ModifyCardPlayResultLocation(
        CardModel card,
        bool isAutoPlay,
        ResourceInfo resources,
        CardLocation location
    )
    {
        if (ReferenceEquals(card, this))
        {
            GuCardPileSystem.Initialize();
            location.pileType =
                GuCardPileSystem.GetResultPileAfterActivation(this);
        }

        return location;
    }

    /// <summary>
    /// 本命蛊固定属于角色普通卡池，避免 CardModel.Pool 扫描 MockCardPool。
    /// </summary>
    public override CardPoolModel Pool =>
        ModelDb.CardPool<GuZhenRenGuCardPool>();

    
    /// <summary>
    /// 当前卡牌使用的卡图资源。
    /// 图片文件名默认与具体运行时卡牌类型名称一致。
    /// </summary>


/// <summary>
    /// 保存每张本命蛊卡牌各自的当前品阶。
    ///
    /// 键的类型是 CardModel，表示状态附加在卡牌实例上。
    /// 值的类型是 int，表示当前品阶。
    ///
    /// 默认值为 1；本命蛊不存在零转状态。
    /// </summary>
    private static readonly SavedAttachedState<CardModel, int>
        GuRankState = new(
            /*
             * 状态保存键。
             *
             * 应当保证在所有 Mod 中唯一。
             */
            "gu_zhen_ren.ben_ming_gu_rank",

            /*
             * 新卡牌第一次读取状态时的默认值。
             */
            () => 1
        );

    /// <summary>
    /// 当前本命蛊能够达到的最高品阶。
    ///
    /// 默认最高九转。
    ///
    /// 具体子类可以覆盖这个属性，
    /// 让特殊本命蛊拥有不同的最高品阶。
    /// </summary>
    public virtual int MaxGuRank => 9;

    /// <summary>
    /// 获取当前卡牌实例的本命蛊品阶。
    ///
    /// 该值来自可保存的 GuRankState。
    /// </summary>
    public int GuRank =>
        Math.Clamp(
            GuRankState[this],
            1,
            Math.Max(1, MaxGuRank)
        );

    /// <summary>
    /// 具体子类额外需要添加的卡牌标签。
    ///
    /// 例如：
    /// - Strike
    /// - Starter
    /// - 自定义流派标签
    ///
    /// 默认没有额外标签。
    /// </summary>
    protected virtual IEnumerable<CardTag> AdditionalTags => [];

    /// <summary>
    /// 每张本命蛊自动包含 BenMingGu 标签。
    /// 仙蛊标签和“唯一”关键词由运行时补丁按实时品阶补充。
    /// </summary>
    protected override HashSet<CardTag> CanonicalTags =>
    [
        GuZhenRenTags.BenMingGu,
        .. AdditionalTags
    ];

    public bool IsXianGu()
    {
        return GuZhenRenCardRules.IsXianGu(this);
    }

    /// <summary>
    /// 本命蛊抽象父类构造器。
    ///
    /// 具体卡牌调用这个构造器时，
    /// 需要提供费用、类型、稀有度和目标类型。
    /// </summary>
    /// <param name="baseCost">
    /// 卡牌基础费用。
    /// </param>
    /// <param name="type">
    /// 卡牌类型，例如攻击、技能或能力。
    /// </param>
    /// <param name="rarity">
    /// 卡牌稀有度。
    /// </param>
    /// <param name="target">
    /// 卡牌目标类型。
    /// </param>
    /// <param name="showInCardLibrary">
    /// 是否在卡牌图鉴中显示。
    /// </param>
    protected AbstractBenMingGuCard(
        int baseCost,
        CardType type,
        CardRarity rarity,
        TargetType target,
        bool showInCardLibrary = true
    ) : base(
        // 将基础参数继续传递给 RitsuLib 卡牌模板。
        baseCost,
        type,
        rarity,
        target,
        showInCardLibrary
    )
    {
    }

    /// <summary>
    /// 判断当前本命蛊是否还可以继续升转。
    /// </summary>
    /// <returns>
    /// 当前品阶小于最高品阶时返回 true。
    /// </returns>
    public bool CanIncreaseGuRank()
    {
        return GuRank < MaxGuRank;
    }

    /// <summary>
    /// 尝试让当前本命蛊提升一个品阶。
    ///
    /// 这是本命蛊多次升转系统的主要入口。
    ///
    /// 可以由以下内容调用：
    /// - 自定义篝火选项；
    /// - 事件；
    /// - 卡牌效果；
    /// - 遗物效果；
    /// - 永久升转奖励。
    /// </summary>
    /// <returns>
    /// 成功升转返回 true。
    /// 已达到最高品阶则返回 false。
    /// </returns>
    public bool TryIncreaseGuRank()
    {
        if (!CanIncreaseGuRank())
        {
            return false;
        }

        int previousGuRank = GuRank;
        int newGuRank = previousGuRank + 1;

        bool committed =
            GuZhenRenCardRules.TryCommitGuRankIncrease(
                this,
                newGuRank,
                () =>
                {
                    GuRankState[this] = newGuRank;

                    // 只触发升转效果，不触发游戏原生 OnUpgrade。
                    OnGuRankIncreased(
                        previousGuRank,
                        newGuRank
                    );
                }
            );

        if (!committed)
        {
            Entry.Logger.Info(
                $"阻止 {Id} 升至 {newGuRank} 转：" +
                "多人唯一性仲裁由另一张同名仙蛊获胜。"
            );
        }

        return committed;
    }

    /// <summary>
    /// 多人唯一性仲裁专用：将冲突的同名仙蛊恢复到五转。
    /// </summary>
    internal void ReconcileGuRankForUniqueness(int rank)
    {
        GuRankState[this] = Math.Clamp(
            rank,
            1,
            Math.Max(1, MaxGuRank)
        );
    }

    /// <summary>
    /// 本命蛊成功升转后的专用效果。
    ///
    /// 具体卡牌仍可独立重写 OnUpgrade，
    /// 实现游戏原生卡牌升级效果。
    /// </summary>
    protected virtual void OnGuRankIncreased(
        int previousGuRank,
        int newGuRank
    )
    {
    }

    /// <summary>
    /// 当永久牌组中的一张卡即将被删除时调用。
    ///
    /// 该方法来自 AbstractModel。
    /// Hook.BeforeCardRemoved 会把被删除的卡牌作为参数传入。
    /// </summary>
    /// <param name="card">
    /// 本次即将从永久牌组中删除的卡牌。
    /// </param>
    public override async Task BeforeCardRemoved(
        CardModel card
    )
    {
        /*
         * Hook 会将删卡通知发送给多个模型。
         *
         * 因此必须确认：
         * 当前接收回调的本命蛊对象，
         * 就是本次即将被删除的那张卡。
         */
        if (!ReferenceEquals(this, card))
        {
            return;
        }

        /*
         * 杀招组并时，材料本命蛊会正常地从永久牌组中移除。
         *
         * 这种删除属于合成流程的一部分，
         * 不应触发本命蛊被毁的生命损失惩罚。
         */
        if (ShaZhaoSynthesisScope.IsActive ||
            GuHeLianScope.IsActive)
        {
            return;
        }


        /*
         * card.Owner 就是拥有这张牌的玩家。
         *
         * 回调发生在 RemoveFromCurrentPile 和
         * RemoveFromState 之前，所以此时 Owner、
         * Pile 和 RunState 仍然可以正常访问。
         */
        var owner = card.Owner;

        /*
         * 执行具体的本命蛊删除惩罚。
         *
         * 生命字段和扣血命令需要按照当前
         * Creature 类的实际 API 实现。
         */
        await ApplyRemovalPenalty(owner);
    }

    /// <summary>
    /// 执行本命蛊被永久删除时的惩罚。
    /// </summary>
    private static async Task ApplyRemovalPenalty(
        Player owner
    )
    {
        // 获取玩家对应的生物对象。
        var creature = owner.Creature;

        // 按最大生命 80% 且至少保留 1 点生命的规则计算损失。
        int hpLoss = CalculateRemovalHpLoss(
            creature.MaxHp,
            creature.CurrentHp
        );

        // 当前只剩 1 点生命等情况下，不执行伤害命令。
        if (hpLoss <= 0)
        {
            return;
        }

        /*
         * 使用事件中同类生命损失的处理方式：
         *
         * Unblockable：
         * 不能被格挡值抵消。
         *
         * Unpowered：
         * 不受普通伤害力量等数值修正影响。
         *
         * null, null：
         * 本次生命损失没有攻击者和卡牌来源。
         */
        await CreatureCmd.Damage(
            new ThrowingPlayerChoiceContext(),
            creature,
            hpLoss,
            ValueProp.Unblockable | ValueProp.Unpowered,
            null,
            null
        );
    }
    /// <summary>
    /// 计算本命蛊被永久删除时需要失去的生命值。
    ///
    /// 原定损失为最大生命值的 80%，
    /// 但本次损失不会让玩家死亡，至少保留 1 点生命。
    /// </summary>
    /// <param name="maxHp">
    /// 玩家最大生命值。
    /// </param>
    /// <param name="currentHp">
    /// 玩家当前生命值。
    /// </param>
    /// <returns>
    /// 实际应失去的生命值。
    /// </returns>
    public static int CalculateRemovalHpLoss(
        int maxHp,
        int currentHp
    )
    {
        /*
         * 最大生命数据无效，或玩家已经只剩 1 点生命时，
         * 不再造成生命损失。
         */
        if (maxHp <= 0 || currentHp <= 1)
        {
            return 0;
        }

        /*
         * 计算最大生命值的 80%。
         *
         * Math.Floor 表示向下取整，
         * 与尖塔1中把 float 强制转换成 int 的行为一致。
         */
        int requestedLoss = (int)Math.Floor(
            maxHp * 0.8m
        );

        // 正常情况下至少失去 1 点生命。
        requestedLoss = Math.Max(
            1,
            requestedLoss
        );

        /*
         * 为了至少保留 1 点生命，
         * 最多只能失去 currentHp - 1 点。
         */
        int maximumSafeLoss = currentHp - 1;

        return Math.Min(
            requestedLoss,
            maximumSafeLoss
        );
    }

}
