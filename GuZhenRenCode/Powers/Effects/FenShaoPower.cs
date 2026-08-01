using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Godot;

using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Combat.HealthBars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Powers;

/// <summary>
/// 焚烧。
///
/// 规则与尖塔1版本保持一致：
///
/// 1. 首次施加时，立即造成等同于当前层数的伤害；
/// 2. 正向叠加后，立即造成等同于叠加后总层数的伤害；
/// 3. 层数降低但仍大于 0 时，再造成等同于剩余层数的伤害；
/// 4. 非玩家拥有者在其回合开始时层数减半；
/// 5. 玩家拥有者在玩家回合结束时层数减半；
/// 6. 生命条显示 Amount / 2 的橙色伤害预测。
/// </summary>
[RegisterPower]
public sealed class FenShaoPower
    : ModPowerTemplate,
      IHealthBarForecastSource
{
    private static readonly Color
        FireOrange =
            new(
                1.0f,
                0.65f,
                0.0f,
                1.0f
            );

    public override PowerType Type =>
        PowerType.Debuff;

    public override PowerStackType StackType =>
        PowerStackType.Counter;

    /// <summary>
    /// Power 图标资源。
    ///
    /// 实际 Godot 地址：
    /// res://GuZhenRen/images/powers/FenShaoPower.png
    /// res://GuZhenRen/images/powers/FenShaoPower_p.png
    /// </summary>
    public override PowerAssetProfile AssetProfile =>
        new(
            IconPath:
                $"{Entry.ResPath}/images/powers/FenShaoPower_p-64x88.png",
            BigIconPath:
                $"{Entry.ResPath}/images/powers/FenShaoPower_p-256x353.png"
        );

    /// <summary>
    /// 初次施加、正向叠加和减层后的即时伤害。
    /// </summary>
    public override async Task
        AfterPowerAmountChanged(
            PlayerChoiceContext choiceContext,
            PowerModel power,
            decimal amount,
            Creature? applier,
            CardModel? cardSource
        )
    {
        if (!ReferenceEquals(
                power,
                this
            ) ||
            Amount <= 0)
        {
            return;
        }

        Flash();

        await TriggerBurnDamageAsync(
            choiceContext,
            Amount
        );

        // 只有正向新增焚烧时才触发星火传播。
        if (amount <= 0m ||
            XingHuoSpreadContext.IsActive ||
            Owner.IsDead)
        {
            return;
        }

        XingHuoLiaoYuanPower? xingHuo =
            Owner.GetPower<
                XingHuoLiaoYuanPower
            >();

        if (xingHuo == null)
        {
            return;
        }

        await xingHuo.TriggerSpreadAsync(
            choiceContext,
            (int)amount
        );
    }

    /// <summary>
    /// 怪物拥有焚烧时，在其所在敌方回合开始后减半。
    /// </summary>
    public override async Task
        AfterSideTurnStart(
            CombatSide side,
            IReadOnlyList<Creature>
                participants,
            ICombatState combatState
        )
    {
        if (Owner.IsPlayer ||
            side != Owner.Side ||
            !participants.Contains(Owner))
        {
            return;
        }

        await HalveAsync(
            new ThrowingPlayerChoiceContext()
        );
    }

    /// <summary>
    /// 玩家拥有焚烧时，在玩家回合结束前减半。
    /// </summary>
    public override async Task
        BeforeSideTurnEnd(
            PlayerChoiceContext choiceContext,
            CombatSide side,
            IEnumerable<Creature>
                participants
        )
    {
        if (!Owner.IsPlayer ||
            side != CombatSide.Player ||
            !participants.Contains(Owner))
        {
            return;
        }

        await HalveAsync(
            choiceContext
        );
    }

    /// <summary>
    /// RitsuLib 生命条覆盖。
    ///
    /// 与尖塔1 HealthBarRenderPower 一致，
    /// 显示 Amount / 2 的橙色预测区域。
    /// </summary>
    public IEnumerable<
        HealthBarForecastSegment
    > GetHealthBarForecastSegments(
        HealthBarForecastContext context
    )
    {
        int forecastAmount =
            Amount / 2;

        if (forecastAmount <= 0)
        {
            return Array.Empty<
                HealthBarForecastSegment
            >();
        }

        return HealthBarForecasts.Single(
            forecastAmount,
            FireOrange,
            HealthBarForecastGrowthDirection
                .FromLeft
        );
    }

    private async Task
        TriggerBurnDamageAsync(
            PlayerChoiceContext choiceContext,
            int damageAmount
        )
    {
        if (damageAmount <= 0 ||
            Owner.IsDead)
        {
            return;
        }

        // 尖塔1中：
        // - 怪物受到焚烧时，伤害来源为玩家；
        // - 玩家受到焚烧时，伤害来源为空。
        Creature? dealer =
            Owner.IsMonster
                ? Applier
                : null;

        await CreatureCmd.Damage(
            choiceContext,
            Owner,
            damageAmount,
            ValueProp.Unpowered |
                ValueProp.SkipHurtAnim,
            dealer,
            cardSource: null,
            cardPlay: null
        );
    }

    private async Task HalveAsync(
        PlayerChoiceContext choiceContext
    )
    {
        if (Amount <= 0 ||
            Owner.IsDead)
        {
            return;
        }

        int targetAmount =
            Amount / 2;

        if (targetAmount <= 0)
        {
            await PowerCmd.Remove(
                this
            );
            return;
        }

        int reduction =
            Amount - targetAmount;

        await PowerCmd.ModifyAmount(
            choiceContext,
            this,
            -reduction,
            Applier,
            cardSource: null
        );
    }
}
