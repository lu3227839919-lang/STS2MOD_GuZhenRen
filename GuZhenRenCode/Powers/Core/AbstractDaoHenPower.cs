using System;
using System.Threading.Tasks;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Powers;

/// <summary>
/// 所有“道痕”能力的公共父类。
///
/// 共同规则：
///
/// 1. 道痕是可叠加的正面能力；
/// 2. 玩家回合开始时，当前具体道痕转化为同层变化道道痕；
/// 3. 转化成功后触发“转移”；
/// 4. 部分道痕可以维护伴生原版能力，例如力量或荆棘。
///
/// 尖塔1使用全局静态 isDerivedPower 防止伴生能力被误减。
/// 本移植版改为实例级保护标记，避免多人和异步流程互相干扰。
/// </summary>
public abstract class AbstractDaoHenPower
    : ModPowerTemplate
{
    /// <summary>
    /// 道痕统一是正面能力。
    /// </summary>
    public override PowerType Type =>
        PowerType.Buff;

    /// <summary>
    /// 道痕层数可以叠加。
    /// </summary>
    public override PowerStackType StackType =>
        PowerStackType.Counter;

    /// <summary>
    /// 当前是否正在由本能力主动调整伴生能力。
    ///
    /// 该标记只属于当前道痕实例，不是全局静态状态。
    /// </summary>
    private bool _isChangingDerivedPower;

    /// <summary>
    /// 伴生能力类型。
    ///
    /// 没有伴生能力的道痕保持 null。
    /// </summary>
    protected virtual Type? DerivedPowerType =>
        null;

    /// <summary>
    /// 当前道痕应保护的伴生能力最低层数。
    /// </summary>
    protected virtual int RequiredDerivedPowerAmount =>
        0;

    /// <summary>
    /// 玩家回合开始时，把具体道痕转回变化道道痕。
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

        // 变化道自身不需要再次转化。
        if (this is BianHuaDaoDaoHenPower)
        {
            return;
        }

        int amount = Amount;
        Creature owner = Owner;

        if (amount <= 0)
        {
            return;
        }

        Flash();

        // 先移除当前道痕。
        // 伴生能力会在各子类 AfterRemoved 中同步扣除。
        await PowerCmd.Remove(this);

        // 再施加同层变化道。
        await PowerCmd.Apply<BianHuaDaoDaoHenPower>(
            choiceContext,
            owner,
            amount,
            owner,
            cardSource: null
        );

        // 触发“转移”获得格挡。
        await ZhuanYiPower.TriggerAsync(
            choiceContext,
            owner
        );
    }

    /// <summary>
    /// 防止外部效果把伴生能力降低到当前道痕应保障的层数以下。
    ///
    /// 当前道痕自身同步减少伴生能力时，不进行拦截。
    /// </summary>
    public override bool TryModifyPowerAmountReceived(
        PowerModel canonicalPower,
        Creature target,
        decimal amount,
        Creature? applier,
        out decimal modifiedAmount
    )
    {
        modifiedAmount = amount;

        if (_isChangingDerivedPower ||
            amount >= 0m ||
            !ReferenceEquals(target, Owner) ||
            DerivedPowerType == null ||
            canonicalPower.GetType() != DerivedPowerType)
        {
            return false;
        }

        PowerModel? currentPower =
            target.GetPower(
                canonicalPower.Id
            );

        int currentAmount =
            currentPower?.Amount ?? 0;

        int requiredAmount =
            Math.Max(
                0,
                RequiredDerivedPowerAmount
            );

        decimal minimumAllowedDelta =
            requiredAmount - currentAmount;

        if (amount >= minimumAllowedDelta)
        {
            return false;
        }

        modifiedAmount =
            minimumAllowedDelta;

        return true;
    }

    /// <summary>
    /// 增加或减少伴生能力。
    ///
    /// 正数通过 PowerCmd.Apply 叠加；
    /// 负数只在能力已经存在时通过 ModifyAmount 扣减。
    /// </summary>
    protected async Task ChangeDerivedPowerAsync<TPower>(
        PlayerChoiceContext choiceContext,
        int delta,
        Creature? owner = null
    )
        where TPower : PowerModel
    {
        if (delta == 0)
        {
            return;
        }

        Creature target = owner ?? Owner;

        _isChangingDerivedPower = true;

        try
        {
            if (delta > 0)
            {
                await PowerCmd.Apply<TPower>(
                    choiceContext,
                    target,
                    delta,
                    target,
                    cardSource: null
                );

                return;
            }

            TPower? existing =
                target.GetPower<TPower>();

            if (existing == null)
            {
                return;
            }

            await PowerCmd.ModifyAmount(
                choiceContext,
                existing,
                delta,
                target,
                cardSource: null
            );
        }
        finally
        {
            _isChangingDerivedPower = false;
        }
    }
}
