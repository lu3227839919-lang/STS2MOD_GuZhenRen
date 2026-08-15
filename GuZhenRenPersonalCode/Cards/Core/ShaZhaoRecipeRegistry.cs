using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using GuZhenRen.Characters;

using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRen.Cards.ShaZhao;

/// <summary>
/// 杀招配方注册表。材料按类型构成多重集合，选择顺序不影响匹配；
/// 同类型材料出现多次时，数量仍然必须一致。
///
/// 新增杀招时：
///
/// 1. 继承 AbstractShaZhaoCard；
/// 2. 注册进 GuZhenRenShaZhaoCardPool；
/// 3. 使用 ShaZhaoRecipeAttribute 声明材料。
/// </summary>
public static class ShaZhaoRecipeRegistry
{
    private sealed record Recipe(
        Type ResultCardType,
        IReadOnlyList<Type>
            OrderedMaterialCardTypes
    );

    private static readonly Lazy<
        IReadOnlyList<Recipe>
    > Recipes =
        new(
            DiscoverRecipes,
            isThreadSafe: true
        );

    /// <summary>
    /// 按材料类型和数量匹配杀招配方，忽略玩家选择顺序。
    /// </summary>
    public static bool TryCreateResult(
        IEnumerable<CardModel>
            selectedCards,
        Player owner,
        [NotNullWhen(true)]
        out AbstractShaZhaoCard? result
    )
    {
        return TryCreateResultForTarget(
            selectedCards,
            owner,
            targetCardType: null,
            out result
        );
    }

    /// <summary>
    /// 根据先选定的杀招结果牌匹配材料并创建结果。
    /// targetCardType 为 null 时保留按材料匹配的兼容行为。
    /// </summary>
    public static bool TryCreateResultForTarget(
        IEnumerable<CardModel>
            selectedCards,
        Player owner,
        Type? targetCardType,
        [NotNullWhen(true)]
        out AbstractShaZhaoCard? result
    )
    {
        ArgumentNullException.ThrowIfNull(
            selectedCards
        );
        ArgumentNullException.ThrowIfNull(
            owner
        );

        // 只枚举一次；保存快照时再使用稳定顺序，确保多人端一致。
        CardModel[] orderedMaterials =
            selectedCards.ToArray();

        Recipe? recipe = FindRecipe(
            orderedMaterials,
            targetCardType
        );

        if (recipe == null)
        {
            result = null;
            return false;
        }

        CardModel canonical =
            ModelDb
                .CardPool<
                    GuZhenRenShaZhaoCardPool
                >()
                .AllCards
                .Single(card =>
                    card.GetType() ==
                    recipe.ResultCardType
                );

        /*
         * 战斗中生成的卡牌必须先由当前 CombatState 创建。
         *
         * 旧实现只创建可变副本并手动设置 Owner，虽然卡牌可以进入手牌，
         * 但不会登记进 CombatState.AllCards。之后手动打出时，
         * CardPileCmd.AddDuringManualCardPlay 会因为
         * CombatState.ContainsCard(card) 为 false 而抛出异常。
         *
         * CombatState.CreateCard 会创建可变实例、设置 Owner、登记战斗状态，
         * 并执行 AfterCreated 生命周期。该步骤发生在同步出牌动作中，
         * 多人端会使用同一战斗卡牌及其 SavedAttachedState 数据。
         */
        if (owner.Creature.CombatState is not
            { } combatState)
        {
            result = null;
            return false;
        }

        result =
            (AbstractShaZhaoCard)
            combatState.CreateCard(
                canonical,
                owner
            );

        // 写入稳定材料快照，并将杀招转数设为最高材料转数。
        result.InitializeFromMaterials(
            orderedMaterials
        );

        return true;
    }

    /// <summary>
    /// 只检查材料是否匹配杀招配方，不创建战斗卡牌。
    /// 推演系统用它在创建结果前验证元气费用，避免支付失败时留下
    /// 已登记但不可见的战斗卡牌实例。
    /// </summary>
    public static bool HasMatchingRecipe(
        IEnumerable<CardModel> selectedCards
    )
    {
        ArgumentNullException.ThrowIfNull(selectedCards);
        return FindRecipe(selectedCards.ToArray()) != null;
    }

    /// <summary>
    /// 只检查材料是否匹配指定杀招。
    /// </summary>
    public static bool HasMatchingRecipe(
        IEnumerable<CardModel> selectedCards,
        Type targetCardType
    )
    {
        ArgumentNullException.ThrowIfNull(selectedCards);
        ArgumentNullException.ThrowIfNull(targetCardType);
        return FindRecipe(
            selectedCards.ToArray(),
            targetCardType
        ) != null;
    }

    /// <summary>
    /// 返回当前蛊手牌材料足以完成的杀招结果类型。
    /// </summary>
    public static IReadOnlyList<Type> GetCraftableResultTypes(
        IEnumerable<CardModel> availableMaterials
    )
    {
        ArgumentNullException.ThrowIfNull(availableMaterials);

        Dictionary<Type, int> availableCounts = availableMaterials
            .GroupBy(card => card.GetType())
            .ToDictionary(group => group.Key, group => group.Count());

        return Recipes.Value
            .Where(recipe =>
                ContainsMaterialMultiset(
                    availableCounts,
                    recipe.OrderedMaterialCardTypes
                )
            )
            .Select(recipe => recipe.ResultCardType)
            .Distinct()
            .OrderBy(
                type => type.FullName ?? type.Name,
                StringComparer.Ordinal
            )
            .ToArray();
    }

    /// <summary>
    /// 获取指定杀招的材料类型并集。杀招配方不设最低转数限制。
    /// </summary>
    public static IReadOnlyList<Type> GetMaterialTypesForResult(
        Type resultCardType
    )
    {
        ArgumentNullException.ThrowIfNull(resultCardType);

        return Recipes.Value
            .Where(recipe => recipe.ResultCardType == resultCardType)
            .SelectMany(recipe => recipe.OrderedMaterialCardTypes)
            .Distinct()
            .OrderBy(
                type => type.FullName ?? type.Name,
                StringComparer.Ordinal
            )
            .ToArray();
    }

    /// <summary>
    /// 获取指定杀招的材料数量范围。多条配方时，玩家可在范围内选择
    /// 任意数量，最终仍由完整材料多重集合校验。
    /// </summary>
    public static bool GetMaterialCountRangeForResult(
        Type resultCardType,
        out int minimum,
        out int maximum
    )
    {
        ArgumentNullException.ThrowIfNull(resultCardType);

        int[] counts = Recipes.Value
            .Where(recipe => recipe.ResultCardType == resultCardType)
            .Select(recipe => recipe.OrderedMaterialCardTypes.Count)
            .ToArray();

        if (counts.Length == 0)
        {
            minimum = 0;
            maximum = 0;
            return false;
        }

        minimum = counts.Min();
        maximum = counts.Max();
        return true;
    }

    private static Recipe? FindRecipe(
        IReadOnlyList<CardModel> orderedMaterials,
        Type? targetCardType = null
    )
    {
        Type[] orderedSelectedTypes = Canonicalize(
            orderedMaterials.Select(card => card.GetType())
        );

        return Recipes.Value.FirstOrDefault(
            candidate =>
                (targetCardType == null ||
                 candidate.ResultCardType == targetCardType) &&
                candidate.OrderedMaterialCardTypes.Count ==
                    orderedSelectedTypes.Length &&
                candidate.OrderedMaterialCardTypes.SequenceEqual(
                    orderedSelectedTypes
                )
        );
    }

    /// <summary>
    /// 获取全部杀招配方。
    ///
    /// 返回的材料列表使用稳定的类型名顺序。
    /// </summary>
    public static IReadOnlyList<(
        Type ResultCardType,
        IReadOnlyList<Type>
            OrderedMaterialCardTypes
    )> GetRecipes()
    {
        return Recipes.Value
            .Select(recipe =>
                (
                    recipe.ResultCardType,
                    recipe
                        .OrderedMaterialCardTypes
                )
            )
            .ToArray();
    }

    /// <summary>
    /// 所有配方用到的材料类型并集。保留此接口供旧调用方兼容；新的
    /// 推演流程会先选择结果杀招，再使用 GetMaterialTypesForResult。
    /// </summary>
    public static IReadOnlySet<Type>
        GetMaterialCardTypes()
    {
        return Recipes.Value
            .SelectMany(recipe =>
                recipe.OrderedMaterialCardTypes
            )
            .ToHashSet();
    }

    /// <summary>
    /// 判断已选材料再加上候选蛊虫后，是否仍是某个合法配方的前缀。
    /// 保留此接口供旧调用方兼容。
    /// </summary>
    public static bool CanExtendToRecipe(
        IReadOnlyList<CardModel> selectedCards,
        CardModel candidate
    )
    {
        ArgumentNullException.ThrowIfNull(selectedCards);
        ArgumentNullException.ThrowIfNull(candidate);

        Type[] selectedTypes = Canonicalize(
            selectedCards.Select(card => card.GetType())
        );
        Type candidateType = candidate.GetType();

        foreach (Recipe recipe in Recipes.Value)
        {
            if (FitsPrefix(
                    recipe.OrderedMaterialCardTypes,
                    selectedTypes,
                    candidateType
                ))
            {
                return true;
            }
        }

        return false;
    }

    private static bool FitsPrefix(
        IReadOnlyList<Type> materialTypes,
        IReadOnlyList<Type> selectedTypes,
        Type candidateType
    )
    {
        Dictionary<Type, int> remaining =
            materialTypes
                .GroupBy(type => type)
                .ToDictionary(
                    group => group.Key,
                    group => group.Count()
                );

        foreach (Type selectedType in selectedTypes)
        {
            if (!remaining.TryGetValue(
                    selectedType,
                    out int count
                ) ||
                count <= 0)
            {
                return false;
            }

            remaining[selectedType] = count - 1;
        }

        return remaining.TryGetValue(
                candidateType,
                out int candidateCount
            ) &&
            candidateCount > 0;
    }

    private static bool ContainsMaterialMultiset(
        IReadOnlyDictionary<Type, int> availableCounts,
        IReadOnlyList<Type> requiredTypes
    )
    {
        foreach ((Type type, int requiredCount) in
                 requiredTypes.GroupBy(type => type)
                    .Select(group =>
                        (group.Key, group.Count())))
        {
            if (!availableCounts.TryGetValue(type, out int count) ||
                count < requiredCount)
            {
                return false;
            }
        }

        return true;
    }

    private static IReadOnlyList<Recipe>
        DiscoverRecipes()
    {
        List<Recipe> recipes = [];

        IEnumerable<CardModel> shaZhaoCards =
            ModelDb
                .CardPool<
                    GuZhenRenShaZhaoCardPool
                >()
                .AllCards;

        foreach (
            CardModel canonical
            in shaZhaoCards.OrderBy(
                card =>
                    card.GetType().FullName ??
                    card.GetType().Name,
                StringComparer.Ordinal
            )
        )
        {
            Type resultType =
                canonical.GetType();

            if (resultType.IsAbstract ||
                !typeof(AbstractShaZhaoCard)
                    .IsAssignableFrom(
                        resultType
                    ))
            {
                continue;
            }

            ShaZhaoRecipeAttribute[] attributes =
                resultType
                    .GetCustomAttributes<
                        ShaZhaoRecipeAttribute
                    >(
                        inherit: false
                    )
                    .ToArray();

            foreach (
                ShaZhaoRecipeAttribute attribute
                in attributes
            )
            {
                // 规范化后，A+B 与 B+A 是同一条配方。
                Type[] orderedMaterials =
                    Canonicalize(attribute.MaterialCardTypes);

                Recipe? duplicate =
                    recipes.FirstOrDefault(
                        existing =>
                            existing
                                .OrderedMaterialCardTypes
                                .SequenceEqual(
                                    orderedMaterials
                                )
                    );

                if (duplicate != null)
                {
                    // 兼容旧版本显式声明的正反两条同结果配方。
                    if (duplicate.ResultCardType == resultType)
                    {
                        continue;
                    }

                    throw new InvalidOperationException(
                        "Duplicate ShaZhao recipe: " +
                        $"{duplicate.ResultCardType.FullName} and " +
                        $"{resultType.FullName} use the same materials."
                    );
                }

                recipes.Add(
                    new Recipe(
                        resultType,
                        orderedMaterials
                    )
                );
            }
        }

        return recipes;
    }

    private static Type[] Canonicalize(
        IEnumerable<Type> materialTypes
    ) =>
        materialTypes
            .OrderBy(
                type => type.FullName ?? type.Name,
                StringComparer.Ordinal
            )
            .ToArray();
}
