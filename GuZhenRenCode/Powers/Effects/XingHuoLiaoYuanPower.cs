using System.Linq;
using System.Threading.Tasks;

using GuZhenRen.Multiplayer;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Powers;

/// <summary>
/// 星火燎原。
///
/// 当拥有者获得正数焚烧时，
/// 把本次新增的焚烧传播给其他所有可命中的敌人。
///
/// 传播过程中不会再次触发传播，避免无限递归。
/// 人工制品由 PowerCmd.Apply 的原生能力拦截流程处理。
/// </summary>
[RegisterPower]
public sealed class XingHuoLiaoYuanPower
    : ModPowerTemplate
{
    public override PowerType Type =>
        PowerType.Debuff;

    public override PowerStackType StackType =>
        PowerStackType.Single;

    /// <summary>
    /// Java 原版使用 -1 作为“不显示层数”的哨兵值。
    ///
    /// 尖塔2默认不允许负层数；若不覆写该属性，
    /// 负数 Debuff 会被 PowerModel.GetTypeForAmount 判定成 Buff，
    /// 从而绕过人工制品。
    /// </summary>
    public override bool AllowNegative =>
        true;

    /// <summary>
    /// 不在能力图标上显示层数。
    /// </summary>
    public override int DisplayAmount =>
        -1;

    /// <summary>
    /// Power 图标资源。
    ///
    /// 实际 Godot 地址：
    /// res://GuZhenRen/images/powers/XingHuoLiaoYuanPower.png
    /// res://GuZhenRen/images/powers/XingHuoLiaoYuanPower_p.png
    /// </summary>
    public override PowerAssetProfile AssetProfile =>
        new(
            IconPath:
                $"{Entry.ResPath}/images/powers/{GetType().Name}_p-64x64.png",
            BigIconPath:
                $"{Entry.ResPath}/images/powers/{GetType().Name}_p-256x256.png"
        );

    /// <summary>
    /// 将新增焚烧传播给其他敌人。
    /// </summary>
    internal async Task TriggerSpreadAsync(
        PlayerChoiceContext choiceContext,
        int spreadAmount
    )
    {
        if (spreadAmount <= 0 ||
            XingHuoSpreadContext.IsActive ||
            Owner.IsDead)
        {
            return;
        }

        Creature[] targets =
            GuZhenRenDeterminism.OrderCreatures(
                CombatState
                    .HittableEnemies
                    .Where(target =>
                        !ReferenceEquals(target, Owner)
                    )
            );

        if (targets.Length == 0)
        {
            return;
        }

        Flash();

        using (
            XingHuoSpreadContext.Enter()
        )
        {
            foreach (Creature target in targets)
            {
                await PowerCmd.Apply<
                    FenShaoPower
                >(
                    choiceContext,
                    target,
                    spreadAmount,
                    Applier ?? Owner,
                    cardSource: null
                );
            }
        }
    }
}
