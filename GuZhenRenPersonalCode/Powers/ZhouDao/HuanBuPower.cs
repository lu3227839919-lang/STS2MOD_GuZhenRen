using GuZhenRen.Cards;
using GuZhenRen.Cards.ZhouDao;

using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Utils;

namespace GuZhenRen.Powers.ZhouDao;

/// <summary>
/// 缓步：以一次 AttackCommand 为单位共享固定减伤额度；多段攻击不会
/// 每段重复获得完整减伤。
/// </summary>
[RegisterPower]
public sealed class HuanBuPower : ModPowerTemplate
{

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://GuZhenRenPersonal/images/power/HuanBuPower-64x64.png",
        BigIconPath: "res://GuZhenRenPersonal/images/power/HuanBuPower-256x256.png"
    );
    private static readonly SavedAttachedState<PowerModel, int>
        ReductionState = new(
            Entry.ModId + ".zhou_dao.huan_bu.reduction",
            static () => 0
        );

    private int _remainingReduction;
    private int _pendingReduction;
    private bool _attackActive;

    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    internal int Reduction => ReductionState[this];

    internal void SetReduction(int reduction) =>
        ReductionState[this] = Math.Max(ReductionState[this], reduction);

    public override Task BeforeAttack(AttackCommand command)
    {
        if (ReferenceEquals(command.Attacker, Owner))
        {
            _remainingReduction = Reduction;
            _pendingReduction = 0;
            _attackActive = true;
        }
        return Task.CompletedTask;
    }

    public override decimal ModifyDamageAdditive(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay
    )
    {
        if (!_attackActive ||
            !ReferenceEquals(dealer, Owner) ||
            !props.IsPoweredAttack() ||
            _remainingReduction <= 0 ||
            amount <= 0)
        {
            return 0m;
        }

        int reduction = Math.Min(
            _remainingReduction,
            Math.Max(0, (int)Math.Ceiling(amount))
        );
        _pendingReduction = reduction;
        return -reduction;
    }

    public override Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource
    )
    {
        if (_attackActive && ReferenceEquals(dealer, Owner))
        {
            _remainingReduction = Math.Max(
                0,
                _remainingReduction - _pendingReduction
            );
            _pendingReduction = 0;
        }
        return Task.CompletedTask;
    }

    public override Task AfterAttack(
        PlayerChoiceContext choiceContext,
        AttackCommand command
    )
    {
        if (ReferenceEquals(command.Attacker, Owner))
        {
            _remainingReduction = 0;
            _pendingReduction = 0;
            _attackActive = false;
        }
        return Task.CompletedTask;
    }

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants
    )
    {
        if (side == CombatSide.Enemy)
        {
            await PowerCmd.TickDownDuration(this);
        }
    }
}
