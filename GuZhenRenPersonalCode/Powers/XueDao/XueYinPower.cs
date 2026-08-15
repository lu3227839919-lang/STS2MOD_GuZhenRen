using System.Runtime.CompilerServices;

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
/// 血印：受到施加者的血道蛊攻击牌伤害后，消耗一层血印，
/// 使目标额外失去三点生命。同一个 CardPlay 全场最多返还一点血元。
/// 默认每个目标每次 CardPlay 最多触发一次；实现
/// IBloodMarkPerHitTrigger 的卡牌可明确允许逐段消耗，但仍共享返还上限。
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

    private sealed class GlobalRefundState
    {
        public bool Active;
        public int PlayIndex = -1;
        public bool Granted;
    }

    private static readonly ConditionalWeakTable<
        CardModel,
        GlobalRefundState
    > GlobalRefundStates = new();

    public const int ExtraHpLoss = 3;

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerInstanceType InstanceType =>
        PowerInstanceType.InstancedPerApplier;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://GuZhenRenPersonal//images//power//XueYinPower-64x64.png",
        BigIconPath: "res://GuZhenRenPersonal/images/power/XueYinPower-256x256.png"
    );

    protected override object InitInternalData() => new TriggerState();

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        TriggerState state = GetInternalData<TriggerState>();

        if (cardPlay.Card.Type == CardType.Attack &&
            XueDaoPowerSystem.IsXueDaoEffectCard(cardPlay.Card) &&
            ReferenceEquals(cardPlay.Card.Owner.Creature, Applier))
        {
            state.ActiveCard = cardPlay.Card;
            state.PlayIndex = cardPlay.PlayIndex;
            state.Triggered = false;
            BeginGlobalRefundWindow(cardPlay);
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
            EndGlobalRefundWindow(cardPlay);
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
            !XueDaoPowerSystem.IsXueDaoEffectCard(cardSource) ||
            !ReferenceEquals(dealer, Applier) ||
            !ReferenceEquals(state.ActiveCard, cardSource) ||
            state.PlayIndex != cardSource.CurrentPlayIndex ||
            (!perHit && state.Triggered))
        {
            return;
        }

        state.Triggered = true;
        Flash();

        // 同一次 CardPlay 即使同时命中多个带血印的敌人，
        // 全场也只返还一次血元；各目标仍分别消耗血印并承受额外失血。
        if (TryClaimGlobalRefund(cardSource))
        {
            await XueDaoPowerSystem.GainXueYuan(
                choiceContext,
                cardSource,
                1
            );
        }

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

    private static void BeginGlobalRefundWindow(
        CardPlay cardPlay
    )
    {
        GlobalRefundState state =
            GlobalRefundStates.GetValue(
                cardPlay.Card,
                static _ => new GlobalRefundState()
            );

        if (!state.Active ||
            state.PlayIndex != cardPlay.PlayIndex)
        {
            state.Active = true;
            state.PlayIndex = cardPlay.PlayIndex;
            state.Granted = false;
        }
    }

    private static void EndGlobalRefundWindow(
        CardPlay cardPlay
    )
    {
        if (!GlobalRefundStates.TryGetValue(
                cardPlay.Card,
                out GlobalRefundState? state
            ) ||
            !state.Active ||
            state.PlayIndex != cardPlay.PlayIndex)
        {
            return;
        }

        state.Active = false;
    }

    private static bool TryClaimGlobalRefund(
        CardModel card
    )
    {
        if (!GlobalRefundStates.TryGetValue(
                card,
                out GlobalRefundState? state
            ) ||
            !state.Active ||
            state.PlayIndex != card.CurrentPlayIndex ||
            state.Granted)
        {
            return false;
        }

        state.Granted = true;
        return true;
    }
}
