using MegaCrit.Sts2.Core.Entities.Creatures;

namespace GuZhenRen.Powers;

/// <summary>
/// 蛊真人模组专用的治疗量修改接口。
///
/// 所有治疗量修正能力统一实现此接口。
/// </summary>
public interface IGuZhenRenHealAmountModifier
{
    /// <summary>
    /// 修改一次正数治疗。
    /// </summary>
    decimal ModifyHealAmount(
        Creature creature,
        decimal amount
    );
}
