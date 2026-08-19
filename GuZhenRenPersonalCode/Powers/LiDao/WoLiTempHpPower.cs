using Godot;

using GuZhenRen.Cards.LiDao;
using MegaCrit.Sts2.Core.Commands;

using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Combat.HealthBars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Powers.LiDao;

/// <summary>
/// 万我生成的独立临时生命来源。
/// 每个我力虚影提供 5 点临时生命；每累计损失 5 点该来源临时生命，
/// 消耗 1 个我力虚影。
/// 承伤顺序固定为：格挡及其它更早防护 → 万我临时生命 → 角色真实生命。
/// </summary>
[RegisterPower]
public sealed class WoLiTempHpPower : ModPowerTemplate, IHealthBarVisualGraftSource
{
    private const string TempHpVar = "TempHp";
    private const string LossRemainderVar = "LossRemainder";

    private int _pendingAbsorption;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    // 万我临时生命由血条直接表现，不在角色 Power 栏重复显示。
    protected override bool IsVisibleInternal => false;
    public override int DisplayAmount => Math.Max(0, Amount);

    public int TempHp => Math.Max(0, DynamicVars[TempHpVar].IntValue);

    public int LossRemainder =>
        Math.Max(0, DynamicVars[LossRemainderVar].IntValue);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar(TempHpVar, 0m),
        new DynamicVar(LossRemainderVar, 0m),
    ];

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://GuZhenRenPersonal/images/power/LiDaoBattlePower-64x64.png",
        BigIconPath: "res://GuZhenRenPersonal/images/power/LiDaoBattlePower-256x256.png"
    );

    internal void AddShadows(int count)
    {
        if (count <= 0)
        {
            return;
        }

        DynamicVars[TempHpVar].BaseValue = TempHp + count * 5;
        InvokeDisplayAmountChanged();
    }


    /// <summary>
    /// 将万我的独立临时生命提供给 RitsuLib Visual Graft。
    /// 当 CurrentHp + TempHp 超过 MaxHp 时，由 RitsuLib 扩展血条；
    /// 未超过 MaxHp 时，由 WoLiTempHpHealthBarPatch 在当前生命右侧绘制橙黄色临时生命段。
    /// 两者都只负责视觉显示；实际吸收发生在最终 HP 损失阶段。
    /// </summary>
    public HealthBarVisualGraftMetrics GetHealthBarVisualGraft(
        HealthBarVisualGraftContext context
    )
    {
        if (!ReferenceEquals(context.Creature, Owner) || TempHp <= 0)
        {
            return new HealthBarVisualGraftMetrics(0);
        }

        return new HealthBarVisualGraftMetrics(
            TempHp,
            new Color("FFB52E"),
            null
        );
    }


    /// <summary>
    /// 由 WoLiTempHpDamagePriorityPatch 在最终 HP 损失阶段登记本次吸收量。
    /// 此时格挡与其它更早的防护已经完成，因此万我只优先于真实生命。
    /// </summary>
    internal int PrepareFinalHpLossAbsorption(decimal hpLoss)
    {
        if (hpLoss <= 0m || TempHp <= 0)
        {
            _pendingAbsorption = 0;
            return 0;
        }

        int absorbed = Math.Min(
            TempHp,
            Math.Max(0, (int)Math.Ceiling(hpLoss))
        );

        _pendingAbsorption = absorbed;
        return absorbed;
    }

    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource
    )
    {
        if (!ReferenceEquals(target, Owner) || _pendingAbsorption <= 0)
        {
            _pendingAbsorption = 0;
            return;
        }

        int absorbed = _pendingAbsorption;
        _pendingAbsorption = 0;
        if (absorbed <= 0)
        {
            return;
        }

        int hp = Math.Max(0, TempHp - absorbed);
        int remainder = LossRemainder + absorbed;
        int consumed = 0;
        while (remainder >= 5 && Amount - consumed > 0)
        {
            remainder -= 5;
            consumed++;
        }

        DynamicVars[TempHpVar].BaseValue = hp;
        DynamicVars[LossRemainderVar].BaseValue = remainder;
        if (consumed > 0)
        {
            await PowerCmd.ModifyAmount(
                choiceContext,
                this,
                -consumed,
                Owner,
                cardSource
            );
            await WoLiPhantomSystem.ConsumeShadowsAsync(
                choiceContext,
                Owner.Player!,
                consumed
            );
        }

        InvokeDisplayAmountChanged();
    }

}
