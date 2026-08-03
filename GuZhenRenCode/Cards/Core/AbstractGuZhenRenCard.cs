// 《杀戮尖塔2》的卡牌模型、卡牌类型、标签等基础类型。
using MegaCrit.Sts2.Core.Models;
using GuZhenRen.Characters;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Random; 
// RitsuLib 提供的 Mod 卡牌模板。
using STS2RitsuLib.Scaffolding.Content;

// RitsuLib 可保存、可随模型序列化的附加状态。
using STS2RitsuLib.Utils;

namespace GuZhenRen.Cards;

/// <summary>
/// 蛊真人模组所有卡牌的公共父类。
///
/// 这个类主要保存“蛊真人卡牌体系”共同使用的数据和规则，
/// 例如：
///
/// 1. 卡牌品阶；
/// 2. 卡牌所属流派；
/// 3. 第二魔法值；
/// 4. 焚烧值；
/// 5. 念值；
/// 6. 仙蛊判定；
/// 7. 自定义存档和复制所需的状态。
///
/// 当前类刻意没有直接猜测尖塔2尚未确认的生命周期接口。
/// 因此，战斗能力重算、正式存档、奖励过滤等功能，
/// 目前通过独立方法和虚钩子提供接入点。
/// </summary>
public abstract class AbstractGuZhenRenCard : ModCardTemplate, IGuRankProvider
{
    /// <summary>
    /// 普通蛊虫每次恢复后默认可催动一次。
    /// </summary>
    public virtual int MaxUses => 1;

    protected override bool IsPlayable =>
        GuCardUsageRules.CanUse(this);

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
    /// 真正实现 IGuWormCard 的蛊虫从专属蛊牌堆催动；杀招与虚影虽然
    /// 复用品阶和流派数据，但不是蛊虫，按普通卡牌规则进入手牌与弃牌堆。
    /// </summary>
    public override CardLocation ModifyCardPlayResultLocation(
        CardModel card,
        bool isAutoPlay,
        ResourceInfo resources,
        CardLocation location
    )
    {
        if (ReferenceEquals(card, this) &&
            this is IGuWormCard)
        {
            GuCardPileSystem.Initialize();
            location.pileType =
                GuCardPileSystem.GetResultPileAfterActivation(this);
        }

        return location;
    }

    /// <summary>
    /// 显式返回普通蛊虫卡池。
    ///
    /// STS2 0.107.1 的 CardModel.Pool 会按 ModelDb.AllCardPools 顺序扫描；
    /// 动态注入的模组卡池位于 MockCardPool 之后时，扫描会先触发仅测试模式
    /// 可用的 MockCardPool.GenerateAllCards()，并抛出 “You monster!”。
    /// 模组卡牌已在编译期知道所属卡池，因此不应走该全局扫描回退。
    /// </summary>
    public override CardPoolModel Pool =>
        ModelDb.CardPool<GuZhenRenGuCardPool>();

    protected override void AddExtraArgsToDescription(
        LocString description
    )
    {
        base.AddExtraArgsToDescription(description);
        description.Add("Rank", GuRank);

        if (this is IGuWormCard)
        {
            description.Add("MaxUses", MaxUses);
            description.Add(
                "RemainingUses",
                GuCardUsageRules.GetRemainingUses(this)
            );
        }
    }

    
// =====================================================================
    //  流派枚举
    // =====================================================================

    /// <summary>
    /// 蛊真人世界观中的蛊道分类。
    ///
    /// 枚举顺序与尖塔1 Java 版本保持一致，
    /// 这样后续迁移旧本地化数组、旧存档或序号逻辑时，
    /// 不需要重新调整对应关系。
    /// </summary>
    public enum Dao
    {
        GuangDao,      // 光道
        YanDao,        // 炎道
        LiDao,         // 力道
        JinDao,        // 金道
        TouDao,        // 偷道
        MuDao,         // 木道
        ShiDao,        // 食道
        ShaDao,        // 杀道
        GuDao,         // 骨道
        LuDao,         // 律道
        ZhiDao,        // 智道
        BianHuaDao,    // 变化道
        YinYangDao,    // 阴阳道
        JianDao,       // 剑道
        XueDao,        // 血道
        YunDao,        // 运道
        FengDao,       // 风道
        ZhouDao,       // 宙道
        TuDao,         // 土道
    }

    // =====================================================================
    //  流派与 CardTag 的固定映射
    // =====================================================================

    /// <summary>
    /// 把 Dao 枚举映射到已经注册的 CardTag。
    ///
    /// 这样，业务代码只需要操作 Dao，
    /// 不需要在每个地方重复写 GuZhenRenTags.XxxDao。
    ///
    /// IReadOnlyDictionary 可以避免运行时误修改这份固定映射。
    /// </summary>
    private static readonly IReadOnlyDictionary<Dao, CardTag>
        DaoTags =
            new Dictionary<Dao, CardTag>
            {
                [Dao.GuangDao] = GuZhenRenTags.GuangDao,
                [Dao.YanDao] = GuZhenRenTags.YanDao,
                [Dao.LiDao] = GuZhenRenTags.LiDao,
                [Dao.JinDao] = GuZhenRenTags.JinDao,
                [Dao.TouDao] = GuZhenRenTags.TouDao,
                [Dao.MuDao] = GuZhenRenTags.MuDao,
                [Dao.ShiDao] = GuZhenRenTags.ShiDao,
                [Dao.ShaDao] = GuZhenRenTags.ShaDao,
                [Dao.GuDao] = GuZhenRenTags.GuDao,
                [Dao.LuDao] = GuZhenRenTags.LuDao,
                [Dao.ZhiDao] = GuZhenRenTags.ZhiDao,
                [Dao.BianHuaDao] =
                    GuZhenRenTags.BianHuaDao,
                [Dao.YinYangDao] =
                    GuZhenRenTags.YinYangDao,
                [Dao.JianDao] = GuZhenRenTags.JianDao,
                [Dao.XueDao] = GuZhenRenTags.XueDao,
                [Dao.YunDao] = GuZhenRenTags.YunDao,
                [Dao.FengDao] = GuZhenRenTags.FengDao,
                [Dao.ZhouDao] = GuZhenRenTags.ZhouDao,
                [Dao.TuDao] = GuZhenRenTags.TuDao,
            };

    // =====================================================================
    //  构造函数
    // =====================================================================

    /// <summary>
    /// 创建一张蛊真人体系卡牌。
    /// </summary>
    /// <param name="baseCost">
    /// 卡牌基础费用。
    /// </param>
    /// <param name="type">
    /// 卡牌类型，例如攻击、技能、能力。
    /// </param>
    /// <param name="rarity">
    /// 卡牌稀有度。
    /// </param>
    /// <param name="target">
    /// 卡牌使用目标。
    /// </param>
    /// <param name="showInCardLibrary">
    /// 是否在卡牌图鉴中显示。
    /// </param>
    protected AbstractGuZhenRenCard(
        int baseCost,
        CardType type,
        CardRarity rarity,
        TargetType target,
        bool showInCardLibrary = true
    )
        : base(
            baseCost,
            type,
            rarity,
            target,
            showInCardLibrary
        )
    {
        // 目前没有额外构造逻辑。
        //
        // 具体卡牌通常会在自己的构造函数中继续调用：
        //
        // SetDao(Dao.ZhiDao);
        // SetGuRank(3);
    }

    // =====================================================================
    //  标签
    // =====================================================================

    /// <summary>
    /// 由具体卡牌补充的固定标签。
    ///
    /// 例如：
    ///
    /// - 本命蛊；
    /// - 杀招；
    /// - 虚影复制；
    /// - 某些特殊卡牌类别。
    ///
    /// 默认没有附加标签。
    /// 子类可以重写并返回自己的标签集合。
    /// </summary>
    protected virtual IEnumerable<CardTag>
        AdditionalCanonicalTags =>
            Array.Empty<CardTag>();

    /// <summary>
    /// 构造当前卡牌最终使用的规范标签集合。
    ///
    /// 生成顺序：
    ///
    /// 1. 加入子类声明的固定标签；
    /// 2. 加入当前流派标签。
    ///
    /// 仙蛊标签由运行时补丁按实时品阶补充，
    /// 避免标签缓存导致五转升六转后不刷新。
    ///
    /// 每次读取时都会生成一个新的 HashSet，
    /// 避免外部代码修改共享静态集合。
    /// </summary>
    protected override HashSet<CardTag> CanonicalTags
    {
        get
        {
            // 首先复制具体卡牌自己声明的固定标签。
            HashSet<CardTag> tags =
                new(AdditionalCanonicalTags);

            // 如果已经设置流派，则加入对应的流派标签。
            if (CurrentDao is Dao dao)
            {
                tags.Add(GetDaoTag(dao));
            }

            return tags;
        }
    }

    /// <summary>
    /// 获取某个流派对应的 CardTag。
    /// </summary>
    /// <param name="dao">
    /// 需要查询的流派。
    /// </param>
    /// <returns>
    /// 已注册的对应 CardTag。
    /// </returns>
    public static CardTag GetDaoTag(Dao dao)
    {
        return DaoTags[dao];
    }

    /// <summary>
    /// 判断指定标签是否属于十九种流派标签。
    ///
    /// 该方法对应尖塔1中的 isDaoTag。
    /// 后续在改道、过滤标签或“如意”能力中都会使用。
    /// </summary>
    public static bool IsDaoTag(CardTag tag)
    {
        return DaoTags.Values.Contains(tag);
    }

    // =====================================================================
    //  蛊虫转数与公共基础状态
    // =====================================================================

    /// <summary>
    /// 永久保存每张普通蛊卡的基础转数。
    ///
    /// 状态附加在 CardModel 实例上，并通过模型 SavedProperties
    /// 参与存档、卡牌重建和多人快照序列化。
    /// </summary>
    public const int MinimumGuRank = 1;

    private static readonly SavedAttachedState<CardModel, int>
        BaseGuRankState = new(
            "gu_zhen_ren.normal_gu_base_rank",
            () => MinimumGuRank
        );

    /// <summary>
    /// 永久保存每张普通蛊卡的当前转数。
    ///
    /// 与基础转数分开保存，保留未来增加战斗内临时转数修正
    /// 时所需的状态语义。
    /// </summary>
    private static readonly SavedAttachedState<CardModel, int>
        GuRankState = new(
            "gu_zhen_ren.normal_gu_rank",
            () => MinimumGuRank
        );

    /// <summary>
    /// 奖励牌是否已经完成首次随机赋阶。
    ///
    /// 旧实现使用 GuRank == 0 作为“尚未赋阶”哨兵。现在转数最低为
    /// 一转，因此改用独立、可保存并参与复制的布尔状态。
    /// </summary>
    private static readonly SavedAttachedState<CardModel, bool>
        InitialGuRankAssignedState = new(
            "gu_zhen_ren.normal_gu_initial_rank_assigned",
            () => false
        );

    /// <summary>
    /// 当前卡牌是否仍需要首次奖励赋阶。
    /// </summary>
    internal bool NeedsInitialGuRankAssignment =>
        !InitialGuRankAssignedState[this];

    /// <summary>
    /// 永久保存的基础蛊虫转数。
    ///
    /// 与 CardModel.IsUpgraded 完全无关。
    /// 所有写入都会同步更新模型附加保存状态。
    /// </summary>
    public int BaseGuRank
    {
        get => NormalizeGuRank(
            BaseGuRankState[this]
        );
        protected set =>
            BaseGuRankState[this] =
                NormalizeGuRank(value);
    }

    /// <summary>
    /// 当前蛊虫转数。
    ///
    /// 普通卡牌升级不会改变该值。
    /// 所有写入都会同步更新模型附加保存状态。
    /// </summary>
    public int GuRank
    {
        get => NormalizeGuRank(
            GuRankState[this]
        );
        protected set =>
            GuRankState[this] =
                NormalizeGuRank(value);
    }

    /// <summary>
    /// 当前蛊卡能够达到的最高转数。
    ///
    /// 默认最高九转；特殊蛊卡可以重写此属性。
    /// </summary>
    public virtual int MaxGuRank => 9;

    private int NormalizeGuRank(int rank)
    {
        return Math.Clamp(
            rank,
            MinimumGuRank,
            Math.Max(
                MinimumGuRank,
                MaxGuRank
            )
        );
    }

    /// <summary>
    /// 兼容尖塔1数据结构的基础描述文本。
    ///
    /// 尖塔2正式卡牌文本应优先放入本地化 JSON，
    /// 此字段只保留给动态描述或旧逻辑迁移使用。
    /// </summary>
    public string MyBaseDescription { get; protected set; } =
        string.Empty;

    /// <summary>
    /// 卡牌当前真实流派。
    ///
    /// null 表示尚未设置流派。
    /// </summary>
    public Dao? CurrentDao { get; private set; }

    // =====================================================================
    //  第二魔法值
    // =====================================================================

    /// <summary>
    /// 当前第二魔法值。
    /// </summary>
    public int SecondMagicNumber { get; protected set; } = -1;

    /// <summary>
    /// 基础第二魔法值。
    ///
    /// -1 表示该卡不使用第二魔法值。
    /// </summary>
    public int BaseSecondMagicNumber { get; protected set; } = -1;

    /// <summary>
    /// 升级是否修改过第二魔法值。
    /// </summary>
    public bool UpgradedSecondMagicNumber { get; protected set; }

    /// <summary>
    /// 当前第二魔法值是否与基础值不同。
    /// </summary>
    public bool IsSecondMagicNumberModified { get; protected set; }

    // =====================================================================
    //  焚烧
    // =====================================================================

    /// <summary>
    /// 基础焚烧值。
    ///
    /// -1 表示该卡不使用焚烧变量。
    /// </summary>
    public int BaseFenShao { get; protected set; } = -1;

    /// <summary>
    /// 当前焚烧值。
    /// </summary>
    public int FenShao { get; protected set; } = -1;

    /// <summary>
    /// 当前焚烧值是否经过临时修改。
    /// </summary>
    public bool IsFenShaoModified { get; protected set; }

    /// <summary>
    /// 升级是否修改过焚烧值。
    /// </summary>
    public bool UpgradedFenShao { get; protected set; }

    // =====================================================================
    //  念
    // =====================================================================

    /// <summary>
    /// 基础念值。
    ///
    /// -1 表示该卡不使用念变量。
    /// </summary>
    public int BaseNian { get; protected set; } = -1;

    /// <summary>
    /// 当前念值。
    /// </summary>
    public int Nian { get; protected set; } = -1;

    /// <summary>
    /// 当前念值是否经过临时修改。
    /// </summary>
    public bool IsNianModified { get; protected set; }

    /// <summary>
    /// 升级是否修改过念值。
    /// </summary>
    public bool UpgradedNian { get; protected set; }

    // =====================================================================
    //  流派操作
    // =====================================================================

    /// <summary>
    /// 设置卡牌流派。
    ///
    /// 通常由具体卡牌构造函数调用一次。
    /// 运行中改道也会经过这个方法。
    /// </summary>
    protected void SetDao(Dao dao)
    {
        // 保存旧流派，供变化钩子判断。
        Dao? previousDao = CurrentDao;

        // 更新真实流派。
        CurrentDao = dao;

        // 通知子类或后续适配层。
        OnDaoChanged(
            previousDao,
            dao
        );
    }

    /// <summary>
    /// 在运行过程中改变卡牌流派。
    ///
    /// 例如某个能力、事件或卡牌效果将一张牌改成另一流派。
    /// </summary>
    public void ChangeDao(Dao newDao)
    {
        SetDao(newDao);
    }

    /// <summary>
    /// 判断当前卡牌是否应被视为某个流派。
    ///
    /// 对应尖塔1的“如意”逻辑：
    ///
    /// - 如意未激活：使用真实流派；
    /// - 如意激活：所有流派查询只把卡牌视为剑道。
    /// </summary>
    public bool HasEffectiveDao(
        Dao queriedDao,
        bool ruiYiActive
    )
    {
        return ruiYiActive
            ? queriedDao == Dao.JianDao
            : CurrentDao == queriedDao;
    }

    /// <summary>
    /// 流派发生变化后的扩展钩子。
    ///
    /// 当前默认不执行任何操作。
    /// 后续可以在这里刷新描述、动态变量或 UI。
    /// </summary>
    protected virtual void OnDaoChanged(
        Dao? previousDao,
        Dao newDao
    )
    {
    }

    // =====================================================================
    //  派生数值重算
    // =====================================================================

    /// <summary>
    /// 重算所有蛊真人自定义派生数值。
    ///
    /// 当前逻辑会把以下当前值恢复为基础值：
    ///
    /// - 第二魔法值；
    /// - 焚烧；
    /// - 念。
    ///
    /// 后续如果某个能力会修改这些数值，
    /// 子类可以重写各自的 RecalculateXxx 方法。
    /// </summary>
    public virtual void RecalculateDerivedValues()
    {
        RecalculateSecondMagicNumber();
        RecalculateFenShao();
        RecalculateNian();
    }

    /// <summary>
    /// 重算第二魔法值。
    /// </summary>
    protected virtual void RecalculateSecondMagicNumber()
    {
        // -1 表示当前卡牌不使用该变量。
        if (BaseSecondMagicNumber < 0)
        {
            return;
        }

        // 先恢复基础值。
        SecondMagicNumber = BaseSecondMagicNumber;

        // 当前基础实现恢复后一定相等。
        // 子类重写并施加能力加成后，该标记才可能为 true。
        IsSecondMagicNumberModified =
            SecondMagicNumber != BaseSecondMagicNumber;
    }

    /// <summary>
    /// 重算焚烧值。
    /// </summary>
    protected virtual void RecalculateFenShao()
    {
        if (BaseFenShao < 0)
        {
            return;
        }

        FenShao = BaseFenShao;
        IsFenShaoModified =
            FenShao != BaseFenShao;
    }

    /// <summary>
    /// 重算念值。
    /// </summary>
    protected virtual void RecalculateNian()
    {
        if (BaseNian < 0)
        {
            return;
        }

        Nian = BaseNian;
        IsNianModified =
            Nian != BaseNian;
    }

    // =====================================================================
    //  蛊虫转数设置与升转
    // =====================================================================

    /// <summary>
    /// 设置初始蛊虫转数。
    ///
    /// 仅用于构造、读档或特殊生成，
    /// 不会触发游戏原生 OnUpgrade。
    /// </summary>
    protected virtual void SetGuRank(int amount)
    {
        int normalizedRank =
            NormalizeGuRank(amount);

        BaseGuRank = normalizedRank;
        GuRank = normalizedRank;
        InitialGuRankAssignedState[this] = true;

        OnGuRankChanged();
    }

    internal void InitializeGuRankFromSource(int rank)
    {
        SetGuRank(rank);
    }

    /// <summary>
    /// 使用命中配方的全部合练材料初始化结果牌。
    ///
    /// 该入口位于公共蛊虫父类，因此声明配方的常规蛊虫牌
    /// 也可以作为合练结果，而不要求继承专用合练牌父类。
    /// 结果转数严格等于全部材料中的最高转数。
    /// </summary>
    internal void InitializeFromHeLian(
        IReadOnlyList<CardModel> materials
    )
    {
        ArgumentNullException.ThrowIfNull(materials);

        if (materials.Count < 2)
        {
            throw new ArgumentException(
                "合练至少需要两张材料牌。",
                nameof(materials)
            );
        }

        int highestMaterialRank = materials
            .OfType<IGuRankProvider>()
            .Select(provider =>
                Math.Max(
                    MinimumGuRank,
                    provider.GuRank
                )
            )
            .DefaultIfEmpty(MinimumGuRank)
            .Max();

        SetGuRank(highestMaterialRank);

        OnHeLianCompleted(materials);
    }

    /// <summary>
    /// 卡牌被合练生成并写入转数后的扩展钩子。
    /// </summary>
    protected virtual void OnHeLianCompleted(
        IReadOnlyList<CardModel> materials
    )
    {
    }

    /// <summary>
    /// 独立提升蛊虫转数。
    ///
    /// 不调用 UpgradeInternal、OnUpgrade，
    /// 也不改变 IsUpgraded。
    /// </summary>
    public virtual bool TryIncreaseGuRank(
        int amount = 1
    )
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                amount,
                "升转数量必须大于零。"
            );
        }

        int previousGuRank = BaseGuRank;
        int targetGuRank =
            previousGuRank + amount;

        if (targetGuRank > MaxGuRank)
        {
            return false;
        }

        bool committed =
            GuZhenRenCardRules.TryCommitGuRankIncrease(
                this,
                targetGuRank,
                () =>
                {
                    BaseGuRank = targetGuRank;
                    GuRank = targetGuRank;

                    OnGuRankIncreased(
                        previousGuRank,
                        targetGuRank
                    );
                    OnGuRankChanged();
                }
            );

        if (!committed)
        {
            Entry.Logger.Info(
                $"阻止 {Id} 升至 {targetGuRank} 转：" +
                "多人唯一性仲裁由另一张同名仙蛊获胜。"
            );
        }

        return committed;
    }

    /// <summary>
    /// 将新开局或旧存档中的普通蛊虫牌归一化为至少一转，并标记为
    /// 已完成初始赋阶。返回本次是否修改了底层状态。
    /// </summary>
    internal bool EnsureMinimumGuRank()
    {
        int rawBaseRank = BaseGuRankState[this];
        int rawCurrentRank = GuRankState[this];
        int normalizedRank = NormalizeGuRank(
            Math.Max(
                rawBaseRank,
                rawCurrentRank
            )
        );

        bool changed =
            rawBaseRank != normalizedRank ||
            rawCurrentRank != normalizedRank ||
            !InitialGuRankAssignedState[this];

        BaseGuRank = normalizedRank;
        GuRank = normalizedRank;
        InitialGuRankAssignedState[this] = true;

        if (changed)
        {
            OnGuRankLoaded();
            OnGuRankChanged();
        }

        return changed;
    }

    /// <summary>
    /// 多人唯一性仲裁专用：将已经发生冲突的同名仙蛊恢复到五转。
    /// 不触发升转奖励，只刷新依赖转数的派生状态。
    /// </summary>
    internal void ReconcileGuRankForUniqueness(int rank)
    {
        int normalizedRank =
            NormalizeGuRank(rank);

        BaseGuRank = normalizedRank;
        GuRank = normalizedRank;
        InitialGuRankAssignedState[this] = true;
        OnGuRankLoaded();
        OnGuRankChanged();
    }

    /// <summary>
    /// 升级第二魔法值。
    /// </summary>
    protected void UpgradeSecondMagicNumber(int amount)
    {
        BaseSecondMagicNumber += amount;
        SecondMagicNumber = BaseSecondMagicNumber;
        UpgradedSecondMagicNumber = true;

        // 永久升级后，当前值就是新的基础值，
        // 因此暂时不视为战斗内修改。
        IsSecondMagicNumberModified = false;
    }

    /// <summary>
    /// 升级焚烧值。
    /// </summary>
    protected void UpgradeFenShao(int amount)
    {
        BaseFenShao += amount;
        FenShao = BaseFenShao;
        UpgradedFenShao = true;
        IsFenShaoModified = false;
    }

    /// <summary>
    /// 升级念值。
    /// </summary>
    protected void UpgradeNian(int amount)
    {
        BaseNian += amount;
        Nian = BaseNian;
        UpgradedNian = true;
        IsNianModified = false;
    }

    /// <summary>
    /// 蛊虫成功升转后的专用效果钩子。
    ///
    /// 只由 TryIncreaseGuRank 调用，
    /// 游戏原生 OnUpgrade 不会调用它。
    /// </summary>
    protected virtual void OnGuRankIncreased(
        int previousGuRank,
        int newGuRank
    )
    {
    }

    /// <summary>
    /// 转数变化后的公共刷新钩子。
    /// </summary>
    protected virtual void OnGuRankChanged()
    {
    }

    /// <summary>
    /// 读档或复制后按当前 GuRank 恢复派生状态。
    ///
    /// 与游戏原生 OnUpgrade 无关。
    /// </summary>
    protected virtual void OnGuRankLoaded()
    {
    }

    // =====================================================================
    //  仙蛊判定与奖励唯一
    // =====================================================================

    /// <summary>
    /// 判断当前卡牌是否为仙蛊。
    ///
    /// 只有当前品阶达到六转及以上时才返回 true。
    /// </summary>
    public bool IsXianGu()
    {
        return GuZhenRenCardRules.IsXianGu(this);
    }

    // =====================================================================
    //  存档辅助
    // =====================================================================

    /// <summary>
    /// 捕获当前卡牌需要额外保存的最小状态。
    ///
    /// 与尖塔1的：
    ///
    /// int[] { rank, misc }
    ///
    /// 对应。
    ///
    /// misc 目前由外部调用者传入，
    /// 因为当前尚未确认尖塔2 CardModel 中 misc 的真实成员。
    /// </summary>
    public GuZhenRenCardState CaptureState(int misc)
    {
        return new GuZhenRenCardState(
            GuRank,
            misc
        );
    }

    /// <summary>
    /// 恢复卡牌自定义状态。
    ///
    /// 该方法负责恢复 GuRank 和 BaseGuRank，
    /// 并触发子类数值重算。
    ///
    /// 返回值是应由外部恢复的 misc。
    /// </summary>
    public int RestoreState(
        GuZhenRenCardState state
    )
    {
        int normalizedRank =
            NormalizeGuRank(state.GuRank);

        GuRank = normalizedRank;
        BaseGuRank = normalizedRank;
        InitialGuRankAssignedState[this] = true;

        // 让依赖品阶的子类恢复自身数值。
        OnGuRankLoaded();

        // 恢复第二魔法值、焚烧和念。
        RecalculateDerivedValues();

        // 通知描述或 UI 刷新。
        OnGuRankChanged();

        return state.Misc;
    }

    // =====================================================================
    //  卡牌复制辅助
    // =====================================================================

    /// <summary>
    /// 把蛊真人体系的自定义字段复制到另一张同系卡牌。
    ///
    /// 该方法不负责创建目标卡牌，
    /// 只负责复制父类中维护的状态。
    ///
    /// 后续找到尖塔2真实的“等价复制”生命周期后，
    /// 应在对应 override 中调用本方法。
    /// </summary>
    protected void CopyGuZhenRenStateTo(
        AbstractGuZhenRenCard copy
    )
    {
        ArgumentNullException.ThrowIfNull(copy);

        // 品阶和公共状态。
        int copiedRank = Math.Clamp(
            GuRank,
            MinimumGuRank,
            Math.Max(
                MinimumGuRank,
                copy.MaxGuRank
            )
        );
        copy.BaseGuRank = copiedRank;
        copy.GuRank = copiedRank;
        InitialGuRankAssignedState[copy] =
            InitialGuRankAssignedState[this];
        copy.MyBaseDescription = MyBaseDescription;
        copy.CurrentDao = CurrentDao;

        // 第二魔法值。
        copy.BaseSecondMagicNumber =
            BaseSecondMagicNumber;
        copy.SecondMagicNumber =
            SecondMagicNumber;
        copy.UpgradedSecondMagicNumber =
            UpgradedSecondMagicNumber;
        copy.IsSecondMagicNumberModified =
            IsSecondMagicNumberModified;

        // 焚烧。
        copy.BaseFenShao = BaseFenShao;
        copy.FenShao = FenShao;
        copy.UpgradedFenShao = UpgradedFenShao;
        copy.IsFenShaoModified =
            IsFenShaoModified;

        // 念。
        copy.BaseNian = BaseNian;
        copy.Nian = Nian;
        copy.UpgradedNian = UpgradedNian;
        copy.IsNianModified =
            IsNianModified;

        // 复制完成后重新计算依赖品阶的数据。
        copy.OnGuRankLoaded();
        copy.RecalculateDerivedValues();
        copy.OnGuRankChanged();
    }
    /// <summary>
    /// 尝试在奖励生成时随机赋予蛊虫初始转数。
    ///
    /// 只有当前卡牌尚未完成首次奖励赋阶时才会实际赋值，可避免
    /// 奖励对象重建、重复 Populate 或多人快照恢复时再次推进随机流。
    /// </summary>
    /// <returns>
    /// 成功完成首次赋值时返回 true；
    /// 当前卡牌已经有转数时返回 false。
    /// </returns>
    public bool TryAssignRandomGuRankOnReward(
        Rng rng,
        int totalFloor,
        int minRank = 1,
        int maxRank = 9
    )
    {
        if (InitialGuRankAssignedState[this])
        {
            return false;
        }

        AssignRandomGuRankOnReward(
            rng,
            totalFloor,
            minRank,
            maxRank
        );

        return true;
    }

    /// <summary>
    /// 奖励发放时随机赋予蛊虫初始转数。
    ///
    /// 只应在 InitialGuRankAssignedState=false 时调用——
    /// 具体卡牌不应再在构造函数里手写随机转数，
    /// 转数统一改由奖励发放逻辑（GuRankRewardPatch）决定。
    ///
    /// 走 SetGuRank 而不是 TryIncreaseGuRank：
    /// 这是“生成时确定初始值”，不是“升转”。
    /// </summary>
    public void AssignRandomGuRankOnReward(
        Rng rng,
        int totalFloor,
        int minRank = 1,
        int maxRank = 9
    )
    {
        if (minRank > maxRank)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minRank),
                minRank,
                "最小转数不能大于最大转数。"
            );
        }

        if (MaxGuRank < 1)
        {
            throw new InvalidOperationException(
                "蛊卡最高转数必须至少为一转。"
            );
        }

        int effectiveMinRank = Math.Clamp(
            minRank,
            1,
            MaxGuRank
        );
        int effectiveMaxRank = Math.Clamp(
            maxRank,
            effectiveMinRank,
            MaxGuRank
        );

        /*
         * 奖励蛊卡的概率中心随总爬塔层数平滑上移：
         *
         * 第 1 层：中心 1.0 转
         * 此后每层增加 0.05 转
         * 第 21 层：中心 2.0 转
         * 第 41 层起：中心固定为 3.0 转
         *
         * 中心始终限制在 1.0–3.0 转。
         * 概率中心允许使用浮点数，但最终蛊卡转数仍为整数。
         */
        const double minimumProgressMean = 1.0;
        const double maximumProgressMean = 3.0;
        const double meanIncreasePerFloor = 0.05;

        int normalizedFloor = Math.Max(
            0,
            totalFloor - 1
        );
        double progressionMean = Math.Clamp(
            minimumProgressMean +
            normalizedFloor * meanIncreasePerFloor,
            minimumProgressMean,
            maximumProgressMean
        );
        double mean = Math.Clamp(
            progressionMean,
            (double)effectiveMinRank,
            (double)effectiveMaxRank
        );

        // 默认1–9转规则的标准差固定为2.0。
        // 当仙蛊唯一性临时把上限降到五转时，仍保持原分布宽度，
        // 避免因缩小区间而意外把标准差一起压缩。
        const double stdDev = 2.0;

        /*
         * 游戏的 NextGaussianDouble 并不是直接在 [min,max] 上使用
         * mean/stdDev：它先在 0–1 范围内截断高斯，再映射到 [min,max]。
         * 因此必须把实际“转数尺度”的中心和标准差归一化；旧代码直接
         * 传入 1.0–3.0 的 mean，会只接受分布低尾并把奖励严重推向高转，
         * 从而让大量奖励错误成为仙蛊。
         *
         * 边界扩展半转再四舍五入，仍保留每个整数转数对应的半整数区间。
         */
        double sampleMin =
            effectiveMinRank - 0.5;
        double sampleMax =
            effectiveMaxRank + 0.5;
        double sampleRange =
            sampleMax - sampleMin;

        double normalizedMean = Math.Clamp(
            (mean - sampleMin) / sampleRange,
            0.0,
            1.0
        );
        double normalizedStdDev =
            stdDev / sampleRange;

        double sampledRank = rng.NextGaussianDouble(
            normalizedMean,
            normalizedStdDev,
            sampleMin,
            sampleMax
        );
        int rank = Math.Clamp(
            (int)Math.Round(sampledRank),
            effectiveMinRank,
            effectiveMaxRank
        );

        SetGuRank(rank);
    }
}

/// <summary>
/// 蛊真人卡牌的最小自定义存档结构。
///
/// 与尖塔1保存的：
///
/// int[] { rank, misc }
///
/// 对应。
/// </summary>
/// <param name="GuRank">
/// 卡牌品阶。
/// </param>
/// <param name="Misc">
/// 由具体卡牌自行解释的额外整数状态。
/// </param>
public readonly record struct GuZhenRenCardState(
    int GuRank,
    int Misc
);
