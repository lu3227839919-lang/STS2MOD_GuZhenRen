using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using GuZhenRen.Characters;

using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRen.Cards.ShaZhao;

/// <summary>
/// 杀招有序配方注册表。
///
/// 新增杀招时：
///
/// 1. 继承 AbstractShaZhaoCard；
/// 2. 注册进 GuZhenRenShaZhaoCardPool；
/// 3. 使用 ShaZhaoRecipeAttribute 按顺序声明材料。
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
    /// 按玩家选择顺序匹配杀招配方。
    ///
    /// A→B 只匹配声明为 A、B 的配方；
    /// 不会匹配声明为 B、A 的配方。
    /// </summary>
    public static bool TryCreateResult(
        IEnumerable<CardModel>
            selectedCards,
        Player owner,
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

        // 只枚举一次，完整保留选牌顺序。
        CardModel[] orderedMaterials =
            selectedCards.ToArray();

        Type[] orderedSelectedTypes =
            orderedMaterials
                .Select(card => card.GetType())
                .ToArray();

        Recipe? recipe =
            Recipes.Value.FirstOrDefault(
                candidate =>
                    candidate
                        .OrderedMaterialCardTypes
                        .Count ==
                    orderedSelectedTypes.Length &&
                    candidate
                        .OrderedMaterialCardTypes
                        .SequenceEqual(
                            orderedSelectedTypes
                        )
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

        // 写入有序材料快照，并将杀招转数设为最高材料转数。
        result.InitializeFromMaterials(
            orderedMaterials
        );

        return true;
    }

    /// <summary>
    /// 获取全部杀招配方。
    ///
    /// 返回的材料列表保持声明顺序。
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
                // 直接复制，不排序。
                Type[] orderedMaterials =
                    attribute
                        .MaterialCardTypes
                        .ToArray();

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
                    throw new InvalidOperationException(
                        "Duplicate ordered ShaZhao recipe: " +
                        $"{duplicate.ResultCardType.FullName} and " +
                        $"{resultType.FullName} use the same ordered materials."
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
}
