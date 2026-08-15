using MegaCrit.Sts2.Core.Models;

namespace GuZhenRen.Cards.HeLian;

/// <summary>
/// 声明一条无序合练配方。
///
/// 材料的类型与数量必须完全匹配，但玩家选择顺序不影响结果。
/// 同一结果牌可以声明多条配方，配方材料数量没有固定上限。
/// </summary>
[AttributeUsage(
    AttributeTargets.Class,
    AllowMultiple = true,
    Inherited = false
)]
public sealed class HeLianRecipeAttribute : Attribute
{
    public HeLianRecipeAttribute(
        params Type[] materialCardTypes
    )
    {
        ArgumentNullException.ThrowIfNull(
            materialCardTypes
        );

        if (materialCardTypes.Length < 2)
        {
            throw new ArgumentException(
                "A HeLian recipe must contain at least two material cards.",
                nameof(materialCardTypes)
            );
        }

        foreach (Type materialType in materialCardTypes)
        {
            ArgumentNullException.ThrowIfNull(
                materialType
            );

            if (!typeof(CardModel).IsAssignableFrom(
                    materialType
                ))
            {
                throw new ArgumentException(
                    $"{materialType.FullName} is not a CardModel type.",
                    nameof(materialCardTypes)
                );
            }

            if (!typeof(IGuWormCard)
                .IsAssignableFrom(materialType))
            {
                throw new ArgumentException(
                    $"{materialType.FullName} is not a Gu worm card type.",
                    nameof(materialCardTypes)
                );
            }
        }

        MaterialCardTypes = Array.AsReadOnly(
            (Type[])materialCardTypes.Clone()
        );
    }

    /// <summary>
    /// 配方要求的材料类型。重复类型代表需要多张同名材料。
    /// </summary>
    public IReadOnlyList<Type> MaterialCardTypes
    {
        get;
    }

    /// <summary>
    /// 参与该配方的每张材料蛊最低转数。默认一转。
    /// </summary>
    public int MinimumMaterialRank { get; set; } = 1;
}

/// <summary>
/// 专属合练蛊只有实现此接口时，才允许进入普通卡牌奖励。
/// </summary>
public interface IHeLianCardRewardEligible
{
}
