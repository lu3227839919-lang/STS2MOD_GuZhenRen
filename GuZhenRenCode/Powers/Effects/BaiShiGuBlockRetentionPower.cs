using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Powers;

/// <summary>
/// 白豕蛊六转效果的隐藏计时能力。
///
/// 原生 BarricadePower 负责保留防御；本能力的层数表示
/// 还可以保留多少个未来回合。每次自身玩家回合开始后减少一层，
/// 最后一层结算后移除本能力以及由白豕蛊施加的壁垒。
/// </summary>
[RegisterPower]
public sealed class BaiShiGuBlockRetentionPower
    : ModPowerTemplate
{
    public override PowerType Type =>
        PowerType.Buff;

    public override PowerStackType StackType =>
        PowerStackType.Counter;

    protected override bool IsVisibleInternal =>
        false;

    public override bool ShouldPlayVfx =>
        false;

    public override PowerAssetProfile AssetProfile =>
        new(
            IconPath:
                $"{Entry.ResPath}/images/powers/ShanYaoGainedTrackerPower_p-64x64.png",
            BigIconPath:
                $"{Entry.ResPath}/images/powers/ShanYaoGainedTrackerPower_p-256x256.png"
        );

    /// <summary>
    /// 回合开始时，原生防御清除判定已经完成，
    /// 因此此处可以安全减少持续时间或移除临时壁垒。
    /// </summary>
    public override async Task AfterPlayerTurnStart(
        PlayerChoiceContext choiceContext,
        Player player
    )
    {
        if (!ReferenceEquals(
                player.Creature,
                Owner
            ))
        {
            return;
        }

        if (Amount > 1)
        {
            await PowerCmd.Decrement(
                this
            );

            return;
        }

        BarricadePower? barricade =
            Owner.GetPower<BarricadePower>();

        await PowerCmd.Remove(
            this
        );

        if (barricade != null)
        {
            await PowerCmd.Remove(
                barricade
            );
        }
    }
}
