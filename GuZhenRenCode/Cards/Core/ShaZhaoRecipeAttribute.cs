using MegaCrit.Sts2.Core.Models;

namespace GuZhenRen.Cards.ShaZhao;

/// <summary>
/// 杀招配方声明特性。
///
/// 参数顺序就是配方顺序：
///
/// <code>
/// [ShaZhaoRecipe(
///     typeof(MaterialCardA),
///     typeof(MaterialCardB)
/// )]
/// </code>
///
/// 只匹配玩家依次选择 A、B 的情况；B、A 是另一条配方。
///
/// 规则：
///
/// 1. 材料顺序影响匹配；
/// 2. 重复材料的数量和位置都影响匹配；
/// 3. 同一杀招可以声明多条有序配方；
/// 4. 两张杀招不能声明完全相同的有序配方。
/// </summary>
[AttributeUsage(
    AttributeTargets.Class,
    AllowMultiple = true,
    Inherited = false
)]
public sealed class ShaZhaoRecipeAttribute
    : Attribute
{
    public ShaZhaoRecipeAttribute(
        params Type[] materialCardTypes
    )
    {
        ArgumentNullException.ThrowIfNull(
            materialCardTypes
        );

        if (materialCardTypes.Length == 0)
        {
            throw new ArgumentException(
                "A ShaZhao recipe must contain at least one material card.",
                nameof(materialCardTypes)
            );
        }

        foreach (
            Type materialType
            in materialCardTypes
        )
        {
            ArgumentNullException.ThrowIfNull(
                materialType
            );

            if (!typeof(CardModel)
                .IsAssignableFrom(
                    materialType
                ))
            {
                throw new ArgumentException(
                    $"{materialType.FullName} is not a CardModel type.",
                    nameof(materialCardTypes)
                );
            }
        }

        // 克隆时保留声明顺序，不进行任何排序。
        MaterialCardTypes =
            Array.AsReadOnly(
                (Type[])
                materialCardTypes.Clone()
            );
    }

    /// <summary>
    /// 严格按声明顺序保存的材料类型。
    /// </summary>
    public IReadOnlyList<Type>
        MaterialCardTypes
    {
        get;
    }
}
