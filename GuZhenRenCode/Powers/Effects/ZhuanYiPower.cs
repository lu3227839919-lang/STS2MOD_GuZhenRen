using System.Threading.Tasks;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Powers;

/// <summary>
/// 转移。
///
/// 每当道痕发生变化时，获得等同于本能力层数的格挡。
/// </summary>
[RegisterPower]
public sealed class ZhuanYiPower
    : ModPowerTemplate
{
    public override PowerType Type =>
        PowerType.Buff;

    public override PowerStackType StackType =>
        PowerStackType.Counter;

    /// <summary>
    /// Power 图标资源。
    ///
    /// 实际 Godot 地址：
    /// res://GuZhenRen/images/powers/ZhuanYiPower.png
    /// res://GuZhenRen/images/powers/ZhuanYiPower_p.png
    /// </summary>
    public override PowerAssetProfile AssetProfile =>
        new(
            IconPath:
                $"{Entry.ResPath}/images/powers/{GetType().Name}_p-64x64.png",
            BigIconPath:
                $"{Entry.ResPath}/images/powers/{GetType().Name}_p-256x256.png"
        );

    /// <summary>
    /// 触发指定生物身上的转移能力。
    /// </summary>
    public static async Task TriggerAsync(
        PlayerChoiceContext choiceContext,
        Creature owner
    )
    {
        ZhuanYiPower? power =
            owner.GetPower<ZhuanYiPower>();

        if (power == null ||
            power.Amount <= 0)
        {
            return;
        }

        power.Flash();

        await CreatureCmd.GainBlock(
            owner,
            power.Amount,
            ValueProp.Unpowered,
            cardPlay: null
        );
    }
}
