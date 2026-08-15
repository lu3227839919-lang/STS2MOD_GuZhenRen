using MegaCrit.Sts2.Core.Models;

namespace GuZhenRen.Cards.ShaZhao;

/// <summary>
/// 杀招配方声明特性。
///
/// 参数只声明材料类型与数量，顺序不影响配方：
///
/// <code>
/// [ShaZhaoRecipe(
///     typeof(MaterialCardA),
///     typeof(MaterialCardB)
/// )]
/// </code>
///
/// 玩家依次选择 A、B 或 B、A 都会匹配同一条配方。
///
/// 规则：
///
/// 1. 材料顺序不影响匹配；
/// 2. 重复材料的数量影响匹配；
/// 3. 同一杀招可以声明多条配方；
/// 4. 两张杀招不能声明完全相同的配方。
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

        // 克隆保证特性参数不会被外部修改；注册表负责规范化顺序。
        MaterialCardTypes =
            Array.AsReadOnly(
                (Type[])
                materialCardTypes.Clone()
            );
    }

    /// <summary>
    /// 声明的材料类型。匹配时由注册表按稳定顺序规范化。
    /// </summary>
    public IReadOnlyList<Type>
        MaterialCardTypes
    {
        get;
    }
}
