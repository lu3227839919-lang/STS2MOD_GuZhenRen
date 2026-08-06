using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using GuZhenRen.Characters;
using STS2RitsuLib.Utils;

namespace GuZhenRen.Cards;

/// <summary>
/// 所有杀招牌的公共父类。
///
/// 公共规则：
///
/// 1. 自动拥有“杀招”标签；
/// 2. 杀招转数由组成材料中的最高转数决定；
/// 3. 配方材料顺序会保存在结果牌中；
/// 4. 游戏原生升级与杀招转数完全独立；
/// 5. 不进入普通奖励池；
/// 6. 可以额外标记为仙蛊屋或凡蛊屋。
/// </summary>
public abstract class AbstractShaZhaoCard
    : AbstractGuZhenRenCard
{
    /// <summary>
    /// 杀招牌固定属于杀招隐藏卡池。
    /// </summary>
    public override CardPoolModel Pool =>
        ModelDb.CardPool<GuZhenRenShaZhaoCardPool>();

    public enum GuHouse
    {
        None,
        XianGuWu,
        FanGuWu,
    }

    private static readonly SavedAttachedState<CardModel, string>
        OrderedMaterialTypeNamesState = new(
            "gu_zhen_ren.sha_zhao.material_type_names",
            static () => string.Empty
        );

    private static readonly SavedAttachedState<CardModel, int[]>
        OrderedMaterialRanksState = new(
            "gu_zhen_ren.sha_zhao.material_ranks",
            static () => []
        );

    private static readonly SavedAttachedState<CardModel, int>
        HouseState = new(
            "gu_zhen_ren.sha_zhao.house",
            static () => (int)GuHouse.None
        );

    /// <summary>
    /// 当前杀招所属的蛊屋。
    /// </summary>
    public GuHouse House
    {
        get
        {
            int value = HouseState[this];
            return Enum.IsDefined(typeof(GuHouse), value)
                ? (GuHouse)value
                : GuHouse.None;
        }
        private set => HouseState[this] = (int)value;
    }

    /// <summary>
    /// 组成该杀招的卡牌类型，严格保持玩家选择顺序。
    ///
    /// 例如 A→B 与 B→A 会得到不同的顺序快照。
    /// </summary>
    public IReadOnlyList<Type>
        OrderedMaterialTypes =>
            DecodeMaterialTypeNames(
                OrderedMaterialTypeNamesState[this]
            )
                .Select(ResolveMaterialType)
                .ToArray();

    /// <summary>
    /// 每张组成材料在合成时的转数，顺序与
    /// <see cref="OrderedMaterialTypes"/> 完全一致。
    /// </summary>
    public IReadOnlyList<int>
        OrderedMaterialRanks =>
            OrderedMaterialRanksState[this];

    /// <summary>
    /// 创建一张尚未完成组方的杀招规范模型。
    ///
    /// 初始转数为零；真正通过杀招推演生成时，
    /// 会由 <see cref="InitializeFromMaterials"/> 写入最终转数。
    /// </summary>
    protected AbstractShaZhaoCard(
        int baseCost,
        CardType type,
        TargetType target,
        bool showInCardLibrary = true
    )
        : base(
            baseCost,
            type,
            CardRarity.Rare,
            target,
            showInCardLibrary
        )
    {
        SetGuRank(0);
    }

    // =================================================================
    //  杀招标签
    // =================================================================

    /// <summary>
    /// 具体杀招补充的其他固定标签。
    /// </summary>
    protected virtual IEnumerable<CardTag>
        AdditionalShaZhaoTags =>
            Array.Empty<CardTag>();

    /// <summary>
    /// 所有杀招都强制包含 ShaZhao 标签。
    /// 具体杀招不能绕过该规则。
    /// </summary>
    protected sealed override IEnumerable<CardTag>
        AdditionalCanonicalTags =>
            EnumerateShaZhaoTags();

    private IEnumerable<CardTag>
        EnumerateShaZhaoTags()
    {
        yield return GuZhenRenTags.ShaZhao;

        switch (House)
        {
            case GuHouse.XianGuWu:
                yield return GuZhenRenTags.XianGuWu;
                break;

            case GuHouse.FanGuWu:
                yield return GuZhenRenTags.FanGuWu;
                break;
        }

        foreach (
            CardTag tag
            in AdditionalShaZhaoTags
        )
        {
            yield return tag;
        }
    }

    protected void SetGuHouse(
        GuHouse house
    )
    {
        if (House == house)
        {
            return;
        }

        GuHouse previousHouse = House;
        House = house;

        OnGuHouseChanged(
            previousHouse,
            house
        );
    }

    protected virtual void OnGuHouseChanged(
        GuHouse previousHouse,
        GuHouse newHouse
    )
    {
    }

    // =================================================================
    //  稳定组成快照与转数
    // =================================================================

    /// <summary>
    /// 使用玩家选择的材料初始化杀招。存档字段名保留 Ordered 前缀，
    /// 以兼容旧存档。
    ///
    /// 最终转数等于所有材料 GuRank 的最大值。
    /// 未实现 IGuRankProvider 的材料按零转处理。
    /// </summary>
    internal void InitializeFromMaterials(
        IReadOnlyList<CardModel>
            orderedMaterials
    )
    {
        ArgumentNullException.ThrowIfNull(
            orderedMaterials
        );

        // 稳定排序使不同客户端即使选牌顺序不同，也写入相同附加状态。
        CardModel[] materials = orderedMaterials
            .Select((card, index) => (card, index))
            .OrderBy(
                item => item.card.GetType().FullName ??
                    item.card.GetType().Name,
                StringComparer.Ordinal
            )
            .ThenByDescending(item => GetMaterialRank(item.card))
            .ThenBy(item => item.card.CurrentUpgradeLevel)
            .ThenBy(
                item => item.card.Enchantment?.Id.ToString() ??
                    string.Empty,
                StringComparer.Ordinal
            )
            .ThenBy(item => item.index)
            .Select(item => item.card)
            .ToArray();

        OrderedMaterialTypeNamesState[this] =
            EncodeMaterialTypeNames(
                materials.Select(card =>
                    card.GetType().FullName ??
                    card.GetType().Name
                )
            );

        int[] materialRanks = materials
            .Select(GetMaterialRank)
            .ToArray();
        OrderedMaterialRanksState[this] = materialRanks;

        int synthesisRank =
            materialRanks.Length == 0
                ? 0
                : materialRanks.Max();

        SetGuRank(synthesisRank);

        OnShaZhaoComposed(materials);
    }

    private static string EncodeMaterialTypeNames(
        IEnumerable<string> typeNames
    )
    {
        // SavedAttachedState 不支持 string[]；类型全名不会包含换行，
        // 因此使用单个 string 以换行稳定编码。
        return string.Join("\n", typeNames);
    }

    private static IEnumerable<string> DecodeMaterialTypeNames(
        string encoded
    )
    {
        return string.IsNullOrEmpty(encoded)
            ? Array.Empty<string>()
            : encoded.Split(
                '\n',
                StringSplitOptions.RemoveEmptyEntries
            );
    }

    private static Type ResolveMaterialType(
        string typeName
    )
    {
        if (!string.IsNullOrWhiteSpace(typeName))
        {
            foreach (System.Reflection.Assembly assembly
                     in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type? resolved = assembly.GetType(
                    typeName,
                    throwOnError: false,
                    ignoreCase: false
                );

                if (resolved != null)
                {
                    return resolved;
                }
            }
        }

        Entry.Logger.Info(
            $"杀招材料类型无法恢复：{typeName}"
        );
        return typeof(CardModel);
    }

    /// <summary>
    /// 获取一张材料牌参与杀招合成时的转数。
    /// </summary>
    public static int GetMaterialRank(
        CardModel card
    )
    {
        ArgumentNullException.ThrowIfNull(card);

        return card is IGuRankProvider provider
            ? Math.Max(0, provider.GuRank)
            : 0;
    }

    /// <summary>
    /// 杀招完成组成后的扩展钩子。
    ///
    /// 具体杀招可以读取有序材料，为 A→B 与 B→A
    /// 实现不同的附加效果。
    /// </summary>
    protected virtual void OnShaZhaoComposed(
        IReadOnlyList<CardModel>
            orderedMaterials
    )
    {
    }

    // =================================================================
    //  转数与升级
    // =================================================================

    /// <summary>
    /// 设置杀招转数。
    ///
    /// 该方法被 sealed，确保所有杀招统一使用非负转数。
    /// </summary>
    protected sealed override void SetGuRank(
        int amount
    )
    {
        int normalizedRank =
            Math.Max(0, amount);

        BaseGuRank = normalizedRank;
        GuRank = normalizedRank;

        OnGuRankChanged();
    }

    /// <summary>
    /// 杀招不能通过升转接口改变材料决定的转数。
    /// </summary>
    public sealed override bool TryIncreaseGuRank(
        int amount = 1
    )
    {
        // 杀招转数只由组成材料决定。
        // 具体杀招仍可独立实现 OnUpgrade。
        return false;
    }

    /// <summary>
    /// 读档或复制后保留已有杀招转数，不再强制归零。
    /// </summary>
    protected sealed override void OnGuRankLoaded()
    {
        int normalizedRank =
            Math.Max(0, GuRank);

        BaseGuRank = normalizedRank;
        GuRank = normalizedRank;

        OnShaZhaoStateLoaded();
    }

    protected virtual void OnShaZhaoStateLoaded()
    {
    }

    // =================================================================
    //  杀招生命周期（0.8.0 重设计）
    // =================================================================

    /// <summary>
    /// 杀招生命周期类型，决定材料蛊的封装与返还时机。
    /// </summary>
    public enum ShaZhaoLifecycle
    {
        /// <summary>瞬发：使用一次后消耗，材料立即进入恢复。</summary>
        Instant,

        /// <summary>次数型：可连续使用多次，用尽后消耗并返还材料。</summary>
        Charged,

        /// <summary>阶段型：按阶段推进形态，最终阶段后消耗并返还材料。</summary>
        Staged,

        /// <summary>封印型：本场战斗不返还材料，战斗结束后归还。</summary>
        Sealed,
    }

    private static readonly SavedAttachedState<CardModel, string>
        BoundMaterialIdsState = new(
            "gu_zhen_ren.sha_zhao.bound_material_ids",
            static () => string.Empty
        );

    private static readonly SavedAttachedState<CardModel, int>
        SpentUsesState = new(
            "gu_zhen_ren.sha_zhao.spent_uses",
            static () => 0
        );

    private static readonly SavedAttachedState<CardModel, int>
        StageState = new(
            "gu_zhen_ren.sha_zhao.stage",
            static () => 0
        );

    /// <summary>
    /// 本杀招的生命周期类型，具体杀招重写。
    /// </summary>
    public virtual ShaZhaoLifecycle Lifecycle =>
        ShaZhaoLifecycle.Instant;

    /// <summary>
    /// 次数型杀招的总使用次数。
    /// </summary>
    public virtual int ShaZhaoMaxUses => 1;

    /// <summary>
    /// 阶段型杀招的总阶段数。
    /// </summary>
    public virtual int MaxStages => 1;

    /// <summary>
    /// 已使用次数（次数型）。
    /// </summary>
    public int SpentUses
    {
        get => Math.Clamp(
            SpentUsesState[this],
            0,
            ShaZhaoMaxUses
        );
        private set => SpentUsesState[this] = value;
    }

    /// <summary>
    /// 当前阶段（阶段型，从 0 开始）。
    /// </summary>
    public int CurrentStage
    {
        get => Math.Clamp(
            StageState[this],
            0,
            Math.Max(1, MaxStages)
        );
        private set => StageState[this] = value;
    }

    /// <summary>
    /// 是否仍绑定材料（杀招存在期间材料被封装）。
    /// </summary>
    public bool HasBoundMaterials =>
        BoundMaterialIdsState[this].Length > 0;

    /// <summary>
    /// 组成本杀招的真实材料卡牌（按绑定顺序）。
    /// </summary>
    public IReadOnlyList<CardModel> BoundMaterials
    {
        get
        {
            string encoded = BoundMaterialIdsState[this];
            if (string.IsNullOrEmpty(encoded))
            {
                return Array.Empty<CardModel>();
            }

            return encoded
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(ResolveBoundMaterial)
                .Where(static material => material != null)
                .Cast<CardModel>()
                .ToArray();
        }
    }

    private CardModel? ResolveBoundMaterial(string id)
    {
        if (Owner?.PlayerCombatState is not { } combatState)
        {
            return null;
        }

        return combatState
            .AllCards
            .FirstOrDefault(candidate =>
                string.Equals(
                    candidate.Id.ToString(),
                    id,
                    StringComparison.Ordinal
                )
            );
    }

    /// <summary>
    /// 创建成功后把真实材料封装进本杀招：
    /// 材料打上绑定标记、从蛊虫循环中移出，本杀招记录材料 ID。
    /// </summary>
    internal async Task BindMaterialsAsync(
        IReadOnlyList<CardModel> materials
    )
    {
        if (materials.Count == 0)
        {
            return;
        }

        BoundMaterialIdsState[this] = string.Join(
            '\n',
            materials.Select(material =>
                material.Id.ToString()
            )
        );

        foreach (CardModel material in materials)
        {
            await ShaZhaoTuiYanSystem.MarkMaterialSealedAsync(
                material,
                this
            );
        }
    }

    /// <summary>
    /// 清除杀招上的材料绑定记录（返还材料后调用）。
    /// </summary>
    internal void ClearBoundMaterials()
    {
        BoundMaterialIdsState[this] = string.Empty;
    }

    /// <summary>
    /// 杀招生命周期推进：次数型扣次、阶段型推进；
    /// 达到终态时消耗杀招并返还材料。
    /// 具体杀招在各自 OnPlay 末尾调用。
    /// </summary>
    protected async Task AdvanceLifecycleAsync(
        PlayerChoiceContext choiceContext
    )
    {
        switch (Lifecycle)
        {
            case ShaZhaoLifecycle.Instant:
                await ConsumeAndReturnAsync(choiceContext);
                break;

            case ShaZhaoLifecycle.Charged:
                int spent = SpentUses + 1;
                SpentUses = spent;
                if (spent >= ShaZhaoMaxUses)
                {
                    await ConsumeAndReturnAsync(choiceContext);
                }
                break;

            case ShaZhaoLifecycle.Staged:
                int nextStage = CurrentStage + 1;
                CurrentStage = nextStage;
                if (nextStage >= MaxStages)
                {
                    await ConsumeAndReturnAsync(choiceContext);
                }
                break;

            case ShaZhaoLifecycle.Sealed:
                // 材料本场不返还；杀招按自身规则消耗。
                await CardExhaustCompat.ExhaustAsync(
                    choiceContext,
                    this
                );
                break;
        }
    }

    /// <summary>
    /// 消耗本杀招并把材料送还恢复流程。
    /// </summary>
    protected async Task ConsumeAndReturnAsync(
        PlayerChoiceContext choiceContext
    )
    {
        Player player = Owner;
        await ShaZhaoTuiYanSystem.ReleaseMaterialsAsync(
            choiceContext,
            this,
            player,
            // 消耗牌杀招使用后：材料同样返还——从蛊封存堆飞入恢复堆，
            // 从零恢复并额外 +1 回合恢复；非消耗杀招按常规返还。
            extraRecoveryTurn: CanonicalKeywords.Contains(
                CardKeyword.Exhaust
            )
        );
        await CardExhaustCompat.ExhaustAsync(
            choiceContext,
            this
        );
    }

    /// <summary>
    /// 主动解体（右键）：1 费，材料返回并额外增加 1 回合恢复。
    /// </summary>
    internal async Task<bool> TryDismantleAsync(
        PlayerChoiceContext choiceContext,
        Player player
    )
    {
        if (Lifecycle == ShaZhaoLifecycle.Instant ||
            !HasBoundMaterials ||
            player.PlayerCombatState is not { } combatState ||
            combatState.Energy < 1)
        {
            return false;
        }

        await PlayerCmd.LoseEnergy(1, player);
        await ShaZhaoTuiYanSystem.ReleaseMaterialsAsync(
            choiceContext,
            this,
            player,
            extraRecoveryTurn: true
        );
        await CardExhaustCompat.ExhaustAsync(
            choiceContext,
            this
        );
        return true;
    }

    /// <summary>
    /// 战斗结束兜底：无条件返还全部绑定材料。
    /// </summary>
    internal void ReleaseMaterialsForCombatEnd(Player player)
    {
        ShaZhaoTuiYanSystem.ReleaseMaterialsForCombatEnd(
            this,
            player
        );
    }
}
