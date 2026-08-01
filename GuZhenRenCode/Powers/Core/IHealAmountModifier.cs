using MegaCrit.Sts2.Core.Entities.Creatures;

namespace GuZhenRen.Powers;

/// <summary>
/// 修改生物实际治疗量的接口。
///
/// CreatureCmd.Heal 没有公开的 PowerModel 治疗修改钩子，
/// 因此由统一 Harmony 补丁遍历受治疗者身上的此类能力。
/// </summary>
public interface IHealAmountModifier
{
    /// <summary>
    /// 修改一次正数治疗。
    /// </summary>
    /// <param name="creature">接受治疗的生物。</param>
    /// <param name="amount">当前治疗量。</param>
    /// <returns>修改后的治疗量。</returns>
    decimal ModifyHealAmount(
        Creature creature,
        decimal amount
    );
}
