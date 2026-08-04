using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using GuZhenRen.Characters;

using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRen.Cards.HeLian;

/// <summary>
/// 无序合练配方注册表。
///
/// 配方由结果牌上的 HeLianRecipeAttribute 声明。匹配时只比较
/// 材料卡类型及其数量，玩家选择材料的先后顺序不会影响结果。
/// </summary>
public static class HeLianRecipeRegistry
{
    private sealed record Recipe(
        Type ResultCardType,
        IReadOnlyList<Type> MaterialCardTypes,
        int MinimumMaterialRank
    );

    private static readonly Lazy<IReadOnlyList<Recipe>>
        Recipes = new(
            DiscoverRecipes,
            isThreadSafe: true
        );

    /// <summary>
    /// 根据选中的全部材料匹配唯一配方并创建结果牌。
    /// </summary>
    public static bool TryCreateResult(
        IEnumerable<CardModel> selectedCards,
        Player owner,
        [NotNullWhen(true)]
        out AbstractGuZhenRenCard? result
    )
    {
        ArgumentNullException.ThrowIfNull(
            selectedCards
        );
        ArgumentNullException.ThrowIfNull(owner);

        CardModel[] materials =
            selectedCards.ToArray();

        if (materials.Length < 2 ||
            materials.Any(card =>
                card is not IGuWormCard
            ))
        {
            result = null;
            return false;
        }

        Type[] selectedTypes = materials
            .Select(card => card.GetType())
            .ToArray();

        Recipe? recipe = Recipes.Value
            .FirstOrDefault(candidate =>
                HaveSameMaterialMultiset(
                    candidate.MaterialCardTypes,
                    selectedTypes
                )
            );

        if (recipe == null ||
            materials.Any(card =>
                card is not IGuRankProvider provider ||
                provider.GuRank < recipe.MinimumMaterialRank
            ))
        {
            result = null;
            return false;
        }

        CardModel canonical = ModelDb
            .CardPool<GuZhenRenGuCardPool>()
            .AllCards
            .Single(card =>
                card.GetType() ==
                recipe.ResultCardType
            );

        // 牌组中的卡牌必须先由当前 RunState 创建并登记。
        // 仅调用 canonical.ToMutable() 再设置 Owner 不会把实例加入
        // RunState，随后 CardPileCmd.Add(..., PileType.Deck) 会抛出：
        // "must be added to a RunState before adding it to your deck"。
        AbstractGuZhenRenCard createdResult =
            (AbstractGuZhenRenCard)
                owner.RunState.CreateCard(
                    canonical,
                    owner
                );

        try
        {
            createdResult.InitializeFromHeLian(
                materials
            );
            result = createdResult;
            return true;
        }
        catch
        {
            // CreateCard 已把结果实例加入运行状态；初始化失败时必须
            // 清理该未完成实例，避免留下不可见的悬空卡牌。
            owner.RunState.RemoveCard(
                createdResult
            );
            throw;
        }
    }

    /// <summary>
    /// 根据当前可用材料，返回至少一条可制作配方所需的选牌范围。
    /// 配方本身决定材料数，因此这里没有固定“两张牌”限制。
    /// </summary>
    public static bool TryGetCraftableMaterialCountRange(
        IEnumerable<CardModel> availableMaterials,
        out int minimum,
        out int maximum
    )
    {
        ArgumentNullException.ThrowIfNull(
            availableMaterials
        );

        CardModel[] available = availableMaterials.ToArray();

        int[] craftableCounts = Recipes.Value
            .Where(recipe =>
                ContainsRequiredMaterials(
                    available,
                    recipe.MaterialCardTypes,
                    recipe.MinimumMaterialRank
                )
            )
            .Select(recipe =>
                recipe.MaterialCardTypes.Count
            )
            .ToArray();

        if (craftableCounts.Length == 0)
        {
            minimum = 0;
            maximum = 0;
            return false;
        }

        minimum = craftableCounts.Min();
        maximum = craftableCounts.Max();
        return true;
    }

    /// <summary>
    /// 判断当前材料是否足以完成指定材料数量的至少一条配方。
    /// </summary>
    public static bool HasCraftableRecipe(
        IEnumerable<CardModel> availableMaterials,
        int materialCount
    )
    {
        ArgumentNullException.ThrowIfNull(
            availableMaterials
        );

        if (materialCount < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(materialCount)
            );
        }

        CardModel[] available = availableMaterials.ToArray();

        return Recipes.Value.Any(recipe =>
            recipe.MaterialCardTypes.Count == materialCount &&
            ContainsRequiredMaterials(
                available,
                recipe.MaterialCardTypes,
                recipe.MinimumMaterialRank
            )
        );
    }

    /// <summary>
    /// 判断具体卡牌是否满足至少一条配方的材料类型与最低转数要求。
    /// </summary>
    public static bool IsEligibleMaterialCard(CardModel card)
    {
        ArgumentNullException.ThrowIfNull(card);

        return card is IGuRankProvider provider &&
            Recipes.Value.Any(recipe =>
                recipe.MaterialCardTypes.Contains(card.GetType()) &&
                provider.GuRank >= recipe.MinimumMaterialRank
            );
    }

    /// <summary>
    /// 判断某张牌的具体类型是否出现在任意合练配方中。
    /// </summary>
    public static bool IsRecipeMaterialType(
        Type cardType
    )
    {
        ArgumentNullException.ThrowIfNull(cardType);

        return Recipes.Value.Any(recipe =>
            recipe.MaterialCardTypes.Contains(
                cardType
            )
        );
    }

    /// <summary>
    /// 判断某张牌是否出现在指定材料数量的合练配方中。
    /// </summary>
    public static bool IsRecipeMaterialType(
        Type cardType,
        int recipeMaterialCount
    )
    {
        ArgumentNullException.ThrowIfNull(cardType);

        if (recipeMaterialCount < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(recipeMaterialCount)
            );
        }

        return Recipes.Value.Any(recipe =>
            recipe.MaterialCardTypes.Count ==
                recipeMaterialCount &&
            recipe.MaterialCardTypes.Contains(
                cardType
            )
        );
    }

    /// <summary>
    /// 获取全部配方。材料列表保留声明内容；匹配仍按无序多重集合处理。
    /// </summary>
    public static IReadOnlyList<(
        Type ResultCardType,
        IReadOnlyList<Type> MaterialCardTypes
    )> GetRecipes()
    {
        return Recipes.Value
            .Select(recipe =>
                (
                    recipe.ResultCardType,
                    recipe.MaterialCardTypes
                )
            )
            .ToArray();
    }

    private static IReadOnlyList<Recipe>
        DiscoverRecipes()
    {
        List<Recipe> recipes = [];

        IEnumerable<CardModel> cards = ModelDb
            .CardPool<GuZhenRenGuCardPool>()
            .AllCards;

        foreach (CardModel canonical in cards
            .OrderBy(
                card => card.GetType().FullName ??
                    card.GetType().Name,
                StringComparer.Ordinal
            ))
        {
            Type resultType = canonical.GetType();

            if (resultType.IsAbstract ||
                !typeof(AbstractGuZhenRenCard)
                    .IsAssignableFrom(resultType))
            {
                continue;
            }

            HeLianRecipeAttribute[] attributes =
                resultType.GetCustomAttributes<
                    HeLianRecipeAttribute
                >(inherit: false)
                .ToArray();

            foreach (
                HeLianRecipeAttribute attribute
                in attributes
            )
            {
                Type[] materials = attribute
                    .MaterialCardTypes
                    .ToArray();

                Recipe? duplicate = recipes
                    .FirstOrDefault(existing =>
                        HaveSameMaterialMultiset(
                            existing.MaterialCardTypes,
                            materials
                        )
                    );

                if (duplicate != null)
                {
                    throw new InvalidOperationException(
                        "Duplicate unordered HeLian recipe: " +
                        $"{duplicate.ResultCardType.FullName} and " +
                        $"{resultType.FullName} use the same materials."
                    );
                }

                int minimumMaterialRank = Math.Max(
                    AbstractGuZhenRenCard.MinimumGuRank,
                    attribute.MinimumMaterialRank
                );

                recipes.Add(
                    new Recipe(
                        resultType,
                        Array.AsReadOnly(materials),
                        minimumMaterialRank
                    )
                );
            }
        }

        return recipes;
    }

    private static bool HaveSameMaterialMultiset(
        IReadOnlyList<Type> left,
        IReadOnlyList<Type> right
    )
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        Dictionary<Type, int> counts =
            CountTypes(left);

        foreach (Type type in right)
        {
            if (!counts.TryGetValue(
                    type,
                    out int remaining
                ))
            {
                return false;
            }

            if (remaining == 1)
            {
                counts.Remove(type);
            }
            else
            {
                counts[type] = remaining - 1;
            }
        }

        return counts.Count == 0;
    }

    private static bool ContainsRequiredMaterials(
        IReadOnlyList<CardModel> available,
        IReadOnlyList<Type> required,
        int minimumMaterialRank
    )
    {
        Type[] eligibleTypes = available
            .Where(card =>
                card is IGuRankProvider provider &&
                provider.GuRank >= minimumMaterialRank
            )
            .Select(card => card.GetType())
            .ToArray();

        Dictionary<Type, int> availableCounts =
            CountTypes(eligibleTypes);

        foreach (
            (Type type, int requiredCount)
            in CountTypes(required)
        )
        {
            if (!availableCounts.TryGetValue(
                    type,
                    out int availableCount
                ) ||
                availableCount < requiredCount)
            {
                return false;
            }
        }

        return true;
    }

    private static Dictionary<Type, int> CountTypes(
        IEnumerable<Type> types
    )
    {
        Dictionary<Type, int> counts = [];

        foreach (Type type in types)
        {
            counts[type] = counts.TryGetValue(
                type,
                out int current
            )
                ? current + 1
                : 1;
        }

        return counts;
    }
}
