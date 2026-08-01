using GuZhenRen.Cards;
using GuZhenRen.Cards.ShaZhao;
using GuZhenRen.Characters;
using GuZhenRen.Multiplayer;

using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2RitsuLib;

namespace GuZhenRen.Cards.Basic;

/// <summary>
/// 杀招推演。
///
/// 这张牌是杀招系统的配方启动器。
///
/// 玩家打出本牌后，从手中依次选择若干张其他蛊虫牌作为材料。
/// 选择顺序就是杀招配方顺序：A→B 与 B→A 可以组成不同杀招。
///
/// - 如果有序材料匹配杀招池中的配方：
///   仅支付本牌自身费用，消耗全部材料牌，并获得对应杀招牌；
///
/// - 如果组合错误但剩余能量足够：
///   支付全部材料牌的当前费用，并按照选择顺序依次自动打出；
///
/// - 如果组合错误且剩余能量不足：
///   材料牌保持在手中，玩家受到无法被格挡的反噬伤害。
///
/// 本牌具有以下特殊规则：
///
/// 1. 固有；
/// 2. 保留；
/// 3. 打出后返回手牌；
/// 4. 普通弃牌和消耗命令不能移除本牌；
/// 5. 战斗中每存在一张本牌，就额外增加一个手牌位置，
///    从效果上实现“不占用手牌上限”。
/// </summary>
[RegisterCard(typeof(GuZhenRenShaZhaoCardPool))]
[RegisterCharacterStarterCard(typeof(GuZhenRenCharacter), 1)]//初始牌，加入手牌
public sealed class ShaZhaoTuiYan
    : ModCardTemplate
{
    /// <summary>
    /// 固定返回杀招卡池，避免 STS2 0.107.1 在查找卡池时先实例化
    /// MockCardPool 并触发仅测试模式可用的 MockCanonical。
    /// </summary>
    public override CardPoolModel Pool =>
        ModelDb.CardPool<GuZhenRenShaZhaoCardPool>();

    /// <summary>
    /// 本牌基础能量费用。
    /// </summary>
    private const int BaseCost = 1;

    /// <summary>
    /// 未升级时，能量不足造成的反噬伤害。
    /// </summary>
    private const int BaseBacklashDamage = 5;

    /// <summary>
    /// 升级后，能量不足造成的反噬伤害。
    /// </summary>
    private const int UpgradedBacklashDamage = 2;

    /// <summary>
    /// 创建一张杀招推演。
    /// </summary>
    public ShaZhaoTuiYan()
        : base(
            baseCost: BaseCost,
            type: CardType.Skill,
            rarity: CardRarity.Rare,
            target: TargetType.Self
        )
    {
    }

    /// <summary>
    /// 当前使用模板打击的图片作为占位图。
    ///
    /// 等正式图片完成后，可改为：
    ///
    /// $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    /// </summary>
    public override CardAssetProfile AssetProfile => new(
        PortraitPath:
            $"{Entry.ResPath}/images/cards/{GetType().Name}.png"
    );

    /// <summary>
    /// 杀招推演只能作为角色固定初始牌存在，
    /// 不允许被药水、发现类效果或其他战斗随机生成机制创建。
    /// </summary>
    public override bool CanBeGeneratedInCombat =>
        false;

    /// <summary>
    /// 本牌固有并且回合结束时保留。
    ///
    /// “打出后回手”和“不占手牌上限”由其他生命周期方法与补丁处理。
    /// </summary>
    public override IEnumerable<CardKeyword>
        CanonicalKeywords =>
            base.CanonicalKeywords
                .Append(CardKeyword.Innate)
                .Append(CardKeyword.Retain)
                .Append(CardKeyword.Eternal);

    /// <summary>
    /// 当前实际反噬伤害。
    /// </summary>
    private int BacklashDamage =>
        IsUpgraded
            ? UpgradedBacklashDamage
            : BaseBacklashDamage;

    /// <summary>
    /// 向卡牌描述文本注入反噬伤害动态参数。
    /// </summary>
    protected override void AddExtraArgsToDescription(
        LocString description
    )
    {
        base.AddExtraArgsToDescription(
            description
        );

        description.Add(
            "Backlash",
            BacklashDamage
        );
    }

    /// <summary>
    /// 打出杀招推演后的主要结算流程。
    /// </summary>
    protected override async Task OnPlay(
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

        PlayerCombatState? playerCombatState =
            Owner.PlayerCombatState;

        // 在非战斗环境或所有者尚未完成战斗初始化时，不执行选牌。
        if (playerCombatState == null)
        {
            return;
        }

        // 只允许选择其他可用蛊虫手牌作为材料。
        CardModel[] eligibleCards =
            playerCombatState
                .Hand
                .Cards
                .Where(
                    IsEligibleMaterial
                )
                .ToArray();

        if (eligibleCards.Length == 0)
        {
            return;
        }

        // 允许玩家选择一张到全部符合条件的手牌。
        //
        // RequireManualConfirmation 确保达到最小选择数量后不会立即提交，
        // 玩家仍可继续添加材料。
        CardSelectorPrefs prefs =
            new(
                SelectionScreenPrompt,
                1,
                eligibleCards.Length
            )
            {
                Cancelable = false,
                RequireManualConfirmation = true,
                PretendCardsCanBePlayed = true,
            };

        List<CardModel> selectedCards =
            (
                await CardSelectCmd.FromHand(
                    context:
                        choiceContext,
                    player:
                        Owner,
                    prefs:
                        prefs,
                    filter:
                        IsEligibleMaterial,
                    source:
                        this
                )
            )
            .ToList();

        if (selectedCards.Count == 0)
        {
            return;
        }

        // 先检查是否匹配杀招配方。
        if (ShaZhaoRecipeRegistry.TryCreateResult(
                selectedCards,
                Owner,
                out AbstractShaZhaoCard? shaZhao
            ))
        {
            await ResolveSuccessfulRecipe(
                choiceContext,
                selectedCards,
                shaZhao
            );

            return;
        }

        // 没有匹配到配方时，进入错误组合分支。
        await ResolveFailedRecipe(
            choiceContext,
            selectedCards,
            playerCombatState,
            cardPlay
        );
    }

    /// <summary>
    /// 修改本牌打出后的目标牌堆。
    ///
    /// 普通卡牌会进入弃牌堆或消耗堆；本牌始终返回手牌。
    /// </summary>
    public override CardLocation ModifyCardPlayResultLocation(
        CardModel card,
        bool isAutoPlay,
        ResourceInfo resources,
        CardLocation location
    )
    {
        if (ReferenceEquals(
                card,
                this
            ))
        {
            // 0.110 将结果牌堆、位置和所属玩家统一封装进 CardLocation。
            // 这里只改为手牌堆，保留原玩家与原位置，等价于旧版逻辑。
            location.pileType =
                PileType.Hand;
        }

        return location;
    }

    /// <summary>
    /// 统计指定玩家当前战斗中的杀招推演数量。
    /// </summary>
    internal static int CountCombatCopies(
        Player player
    )
    {
        ArgumentNullException.ThrowIfNull(player);

        return player
            .Piles
            .Where(pile => pile.IsCombatPile)
            .SelectMany(pile => pile.Cards)
            .Count(card => card is ShaZhaoTuiYan);
    }

    /// <summary>
    /// 判断一张手牌能否作为杀招材料。
    /// </summary>
    private static bool IsEligibleMaterial(
        CardModel card
    )
    {
        // 只有蛊虫牌才能作为杀招材料。
        //
        // IGuRankProvider 是当前项目对“蛊虫牌”的统一标记：
        // 普通蛊牌、本命蛊以及初始蛊牌都会实现该接口。
        if (card is not IGuRankProvider)
        {
            return false;
        }

        // 本牌不能选择自身，否则可能形成递归或重复回手问题。
        if (card is ShaZhaoTuiYan)
        {
            return false;
        }

        // X 费蛊虫牌允许作为材料。
        //
        // 若组合错误，它会在后续自动出牌分支中固定按 X = 0 结算，
        // 不消耗玩家的剩余能量。

        // 普通不可打出的牌不能参与错误组合的自动出牌分支。
        if (card.Keywords.Contains(
                CardKeyword.Unplayable
            ))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 结算正确的杀招配方。
    /// </summary>
    private async Task ResolveSuccessfulRecipe(
        PlayerChoiceContext choiceContext,
        IReadOnlyList<CardModel> selectedCards,
        AbstractShaZhaoCard shaZhao
    )
    {
        // 杀招材料的消耗过程处于专用作用域内。
        //
        // 其他系统可以通过 ShaZhaoSynthesisScope 判断当前消耗是否来自
        // 杀招合成，并选择忽略普通“卡牌被消耗”触发。
        using (
            ShaZhaoSynthesisScope.Enter()
        )
        {
            foreach (
                CardModel material
                in selectedCards
            )
            {
                // 正确配方只消耗材料，不支付其费用，也不触发其出牌效果。
                await CardCmd.Exhaust(
                    choiceContext,
                    material
                );
            }
        }

        // 将对应杀招作为临时生成牌加入手牌。
        await CardPileCmd.AddGeneratedCardToCombat(
            shaZhao,
            PileType.Hand,
            Owner
        );
    }

    /// <summary>
    /// 结算错误的杀招配方。
    /// </summary>
    private async Task ResolveFailedRecipe(
        PlayerChoiceContext choiceContext,
        IReadOnlyList<CardModel> selectedCards,
        PlayerCombatState playerCombatState,
        CardPlay cardPlay
    )
    {
        // 在任何材料开始执行前，先固定记录全部材料的当前实际费用。
        //
        // 这样第一张材料牌即使改变能量或其他卡牌费用，也不会改变本次
        // 错误组合原本要求支付的总额。
        (
            CardModel card,
            int energyCost
        )[] materialCosts =
            selectedCards
                .Select(
                    card =>
                        (
                            card,
                            energyCost:
                                card.EnergyCost.CostsX
                                    ? 0
                                    : Math.Max(
                                        0,
                                        card.EnergyCost
                                            .GetWithModifiers(
                                                CostModifiers.All
                                            )
                                    )
                        )
                )
                .ToArray();

        int totalMaterialCost =
            materialCosts.Sum(
                item => item.energyCost
            );

        // 本牌的费用在进入 OnPlay 前已经由正常出牌流程支付。
        //
        // 因此这里只检查玩家剩余能量是否足以支付全部材料牌费用。
        if (playerCombatState.Energy <
            totalMaterialCost)
        {
            // 能量不足时材料仍留在手中，不执行任何材料效果。
            await ApplyBacklash(
                choiceContext,
                cardPlay
            );

            return;
        }

        // 先一次性支付全部材料的捕获费用。
        //
        // 这可以防止某张先执行的材料牌产生能量，导致后续材料实际上
        // 没有付出原本要求的代价。
        foreach (
            (
                CardModel card,
                int energyCost
            ) material
            in materialCosts
        )
        {
            if (material.energyCost > 0)
            {
                await material.card.SpendEnergy(
                    material.energyCost
                );
            }
        }

        // 按玩家选择顺序依次自动打出材料牌。
        foreach (
            (
                CardModel card,
                int energyCost
            ) material
            in materialCosts
        )
        {
            Creature? target =
                ResolveAutoPlayTarget(
                    material.card
                );

            // 群体牌和无目标牌仍允许使用 null。
            //
            // 单体目标牌若在前序材料结算后已经没有合法目标，
            // AutoPlay 通常会因空目标抛出 ArgumentNullException。
            // 此时安全跳过该材料，避免整次推演结算崩溃。
            try
            {
                bool costsX =
                    material.card.EnergyCost.CostsX;

                if (costsX)
                {
                    // AutoPlay 默认会把 X 捕获为玩家当前剩余能量。
                    // 先固定为 0，再通过 skipXCapture 阻止原方法覆盖。
                    material.card.EnergyCost
                        .CapturedXValue = 0;
                }

                await CardCmd.AutoPlay(
                    choiceContext,
                    material.card,
                    target!,
                    skipXCapture: costsX
                );
            }
            catch (ArgumentNullException)
                when (target == null)
            {
                continue;
            }
        }
    }

    /// <summary>
    /// 施加能量不足时的反噬伤害。
    /// </summary>
    private async Task ApplyBacklash(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        await CreatureCmd.Damage(
            choiceContext,
            Owner.Creature,
            BacklashDamage,

            // Unblockable：不能被格挡抵消。
            // Unpowered：不受力量等普通伤害修正影响。
            ValueProp.Unblockable |
                ValueProp.Unpowered,

            dealer: null,
            cardSource: this,
            cardPlay: cardPlay
        );
    }

    /// <summary>
    /// 为错误组合中自动打出的材料牌随机选择一个合法目标。
    /// </summary>
    private Creature? ResolveAutoPlayTarget(
        CardModel card
    )
    {
        var combatState =
            CombatState ??
            Owner.Creature.CombatState;

        if (combatState == null)
        {
            return card.IsValidTarget(
                Owner.Creature
            )
                ? Owner.Creature
                : null;
        }

        // 敌方单体牌从当前所有可命中且满足卡牌目标规则的敌人中
        // 使用独立的 RitsuLib 玩家 RNG 流，避免污染游戏本体随机序列。
        Creature[] validEnemyTargets =
            GuZhenRenDeterminism.OrderCreatures(
                combatState
                    .HittableEnemies
                    .Where(card.IsValidTarget)
            );

        if (validEnemyTargets.Length > 0)
        {
            return RitsuLibFramework
                .GetModPlayerRng(
                    Owner,
                    Entry.ModId,
                    "sha_zhao_tui_yan/auto_target"
                )
                .NextItem(validEnemyTargets);
        }

        // 自身目标牌仍稳定指向使用者。
        if (card.IsValidTarget(
                Owner.Creature
            ))
        {
            return Owner.Creature;
        }

        // 群体牌、无目标牌或当前没有合法单体目标时返回 null。
        return null;
    }

    /// <summary>
    /// 升级本牌。
    ///
    /// 反噬伤害通过 <see cref="IsUpgraded"/> 动态计算，
    /// 因此这里不需要修改额外字段。
    /// </summary>
    protected override void OnUpgrade()
    {
    }
}
