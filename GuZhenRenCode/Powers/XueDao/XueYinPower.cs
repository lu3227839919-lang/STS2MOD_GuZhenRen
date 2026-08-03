using GuZhenRen.Cards;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Powers.XueDao;

/// <summary>
/// 血印：受到施加者的血道蛊攻击牌伤害后，施加者获得一点血元，
/// 消耗一层血印，并使目标额外失去三点生命。
/// 默认每次 CardPlay 最多触发一次，多段攻击不会连续消耗血印；
/// 实现 IBloodMarkPerHitTrigger 的卡牌可明确允许逐段触发。
/// </summary>
[RegisterPower]
public sealed class XueYinPower : ModPowerTemplate
{
    private sealed class TriggerState
    {
        public CardModel? ActiveCard { get; set; }
        public int PlayIndex { get; set; } = -1;
        public bool Triggered { get; set; }
    }

    public const int ExtraHpLoss = 3;

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerInstanceType InstanceType =>
        PowerInstanceType.InstancedPerApplier;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://GuZhenRen//images//power//ZhaoPoPower-64x64.png",
        BigIconPath: "res://GuZhenRen/images/power/ZhaoPoPower-256x256.png"
    );

    protected override object InitInternalData() => new TriggerState();

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        TriggerState state = GetInternalData<TriggerState>();

        if (cardPlay.Card.Type == CardType.Attack &&
            XueDaoPowerSystem.IsXueDaoGuCard(cardPlay.Card) &&
            ReferenceEquals(cardPlay.Card.Owner.Creature, Applier))
        {
            state.ActiveCard = cardPlay.Card;
            state.PlayIndex = cardPlay.PlayIndex;
            state.Triggered = false;
        }

        return Task.CompletedTask;
    }

    public override Task AfterCardPlayed(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        TriggerState state = GetInternalData<TriggerState>();

        if (ReferenceEquals(state.ActiveCard, cardPlay.Card) &&
            state.PlayIndex == cardPlay.PlayIndex)
        {
            state.ActiveCard = null;
            state.PlayIndex = -1;
            state.Triggered = false;
        }

        return Task.CompletedTask;
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
        TriggerState state = GetInternalData<TriggerState>();
        bool perHit = cardSource is IBloodMarkPerHitTrigger;

        if (!ReferenceEquals(target, Owner) ||
            Amount <= 0 ||
            result.UnblockedDamage <= 0 ||
            cardSource?.Type != CardType.Attack ||
            !XueDaoPowerSystem.IsXueDaoGuCard(cardSource) ||
            !ReferenceEquals(dealer, Applier) ||
            !ReferenceEquals(state.ActiveCard, cardSource) ||
            state.PlayIndex != cardSource.CurrentPlayIndex ||
            (!perHit && state.Triggered))
        {
            return;
        }

        state.Triggered = true;
        Flash();

        await XueDaoPowerSystem.GainXueYuan(
            choiceContext,
            cardSource,
            1
        );
        await PowerCmd.Decrement(this);

        if (!target.IsDead)
        {
            await CreatureCmd.Damage(
                choiceContext,
                target,
                ExtraHpLoss,
                ValueProp.Unblockable | ValueProp.Unpowered,
                dealer: null,
                cardSource: null,
                cardPlay: null
            );
        }
    }
}
