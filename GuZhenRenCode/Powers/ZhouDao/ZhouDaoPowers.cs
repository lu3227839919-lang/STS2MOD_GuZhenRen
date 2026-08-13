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


[RegisterPower]
public sealed class ZhouDaoTrackerPower : ModPowerTemplate
{    private static readonly SavedAttachedState<PowerModel, int>
        LastSuiManTurnState = new(
            Entry.ModId + ".zhou_dao.last_sui_man_turn",
            static () => 0
        );

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    protected override bool IsVisibleInternal => false;

    internal int LastSuiManTurn
    {
        get => LastSuiManTurnState[this];
        set => LastSuiManTurnState[this] = value;
    }
}

[RegisterPower]
public sealed class NianHuaPower : ModPowerTemplate
{

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://GuZhenRen/images/power/NianHuaPower-64x64.png",
        BigIconPath: "res://GuZhenRen/images/power/NianHuaPower-256x256.png"
    );
    public const int MaximumAmount = 6;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override bool TryModifyPowerAmountReceived(
        PowerModel canonicalPower,
        Creature target,
        decimal amount,
        Creature? applier,
        out decimal modifiedAmount
    )
    {
        modifiedAmount = amount;
        if (canonicalPower is not NianHuaPower ||
            !ReferenceEquals(target, Owner) ||
            amount <= 0)
        {
            return false;
        }

        modifiedAmount = Math.Min(
            amount,
            Math.Max(0, MaximumAmount - Amount)
        );
        return modifiedAmount != amount;
    }
}

/// <summary>光阴荏苒伴生能力：蛊虫完成恢复时反哺年华。</summary>
[RegisterPower]
public sealed class GuangYinRenRanPower : ModPowerTemplate
{

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://GuZhenRen/images/power/GuangYinRenRanPower-64x64.png",
        BigIconPath: "res://GuZhenRen/images/power/GuangYinRenRanPower-256x256.png"
    );
    private static readonly SavedAttachedState<PowerModel, int>
        RankState = new(
            Entry.ModId + ".zhou_dao.gyrr.rank",
            static () => 1
        );
    private static readonly SavedAttachedState<PowerModel, int>
        TriggerTurnState = new(
            Entry.ModId + ".zhou_dao.gyrr.trigger_turn",
            static () => 0
        );
    private static readonly SavedAttachedState<PowerModel, int>
        TriggerCountState = new(
            Entry.ModId + ".zhou_dao.gyrr.trigger_count",
            static () => 0
        );
    private static readonly SavedAttachedState<PowerModel, int>
        AcceleratedTurnState = new(
            Entry.ModId + ".zhou_dao.gyrr.accelerated_turn",
            static () => 0
        );

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    internal int Rank => Math.Clamp(RankState[this], 1, 9);
    internal void SetRank(int rank) => RankState[this] = Math.Clamp(rank, 1, 9);

    internal async Task OnGuRecoveredAsync(
        PlayerChoiceContext choiceContext,
        bool acceleratedBySuiMan
    )
    {
        Player? player = Owner.Player;
        if (player?.PlayerCombatState is not { } state)
        {
            return;
        }

        int turn = state.TurnNumber;
        if (TriggerTurnState[this] != turn)
        {
            TriggerTurnState[this] = turn;
            TriggerCountState[this] = 0;
        }

        int rank = Rank;
        int limit = rank switch
        {
            <= 2 => 1,
            <= 5 => 2,
            _ => 3,
        };

        if (TriggerCountState[this] < limit)
        {
            TriggerCountState[this]++;
            await ZhouDaoPowerSystem.GainNianHua(
                choiceContext,
                player,
                1,
                sourceCard: null
            );
        }

        if (!acceleratedBySuiMan || rank < 5 ||
            AcceleratedTurnState[this] == turn)
        {
            return;
        }

        AcceleratedTurnState[this] = turn;
        int bonus = rank >= 8 ? 2 : 1;
        await ZhouDaoPowerSystem.GainNianHua(
            choiceContext,
            player,
            bonus,
            sourceCard: null
        );
        if (rank >= 9)
        {
            await CardPileCmd.Draw(choiceContext, 1, player);
        }
    }
}

/// <summary>年年岁岁伴生能力：按回合稳定推动年华。</summary>
[RegisterPower]
public sealed class NianNianSuiSuiPower : ModPowerTemplate
{

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://GuZhenRen/images/power/NianNianSuiSuiPower-64x64.png",
        BigIconPath: "res://GuZhenRen/images/power/NianNianSuiSuiPower-256x256.png"
    );
    private static readonly SavedAttachedState<PowerModel, int>
        RankState = new(Entry.ModId + ".zhou_dao.nnss.rank", static () => 1);
    private static readonly SavedAttachedState<PowerModel, int>
        TurnCounterState = new(
            Entry.ModId + ".zhou_dao.nnss.turn_counter",
            static () => 0
        );
    private static readonly SavedAttachedState<PowerModel, int>
        SuiManCounterState = new(
            Entry.ModId + ".zhou_dao.nnss.sui_man_counter",
            static () => 0
        );

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    internal int Rank => Math.Clamp(RankState[this], 1, 9);
    internal void SetRank(int rank) => RankState[this] = Math.Clamp(rank, 1, 9);

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants
    )
    {
        if (Owner.Player is not { } player || side != Owner.Side)
        {
            return;
        }

        int rank = Rank;
        int counter = ++TurnCounterState[this];
        int gain = rank switch
        {
            1 when counter % 3 == 0 => 1,
            2 or 3 when counter % 2 == 0 => 1,
            4 or 5 or 6 or 7 => 1,
            >= 8 => 2,
            _ => 0,
        };

        if (gain > 0)
        {
            await ZhouDaoPowerSystem.GainNianHua(
                choiceContext,
                player,
                gain,
                sourceCard: null
            );
        }
    }

    internal async Task OnSuiManAsync(
        PlayerChoiceContext choiceContext,
        CardModel? sourceCard
    )
    {
        if (Owner.Player is not { } player)
        {
            return;
        }

        int rank = Rank;
        int count = ++SuiManCounterState[this];
        bool gain = rank switch
        {
            5 => count == 1,
            6 => count % 2 == 0,
            7 or 9 => true,
            _ => false,
        };
        if (gain)
        {
            await ZhouDaoPowerSystem.GainNianHua(
                choiceContext,
                player,
                1,
                sourceCard
            );
        }
    }
}

/// <summary>宙锚伴生能力：岁满后保留一部分年华进度。</summary>
[RegisterPower]
public sealed class ZhouMaoPower : ModPowerTemplate
{

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://GuZhenRen/images/power/ZhouMaoPower-64x64.png",
        BigIconPath: "res://GuZhenRen/images/power/ZhouMaoPower-256x256.png"
    );
    private static readonly SavedAttachedState<PowerModel, int>
        RankState = new(Entry.ModId + ".zhou_dao.zhou_mao.rank", static () => 6);
    private static readonly SavedAttachedState<PowerModel, int>
        SuiManCounterState = new(
            Entry.ModId + ".zhou_dao.zhou_mao.counter",
            static () => 0
        );

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    internal int Rank => Math.Clamp(RankState[this], 6, 9);
    internal void SetRank(int rank) => RankState[this] = Math.Clamp(rank, 6, 9);

    internal async Task OnSuiManAsync(
        PlayerChoiceContext choiceContext,
        CardModel? sourceCard
    )
    {
        if (Owner.Player is not { } player)
        {
            return;
        }

        int rank = Rank;
        int gain = rank switch
        {
            6 => 1,
            7 => 2,
            8 => 2,
            _ => 3,
        };
        int count = ++SuiManCounterState[this];
        await ZhouDaoPowerSystem.GainNianHua(
            choiceContext,
            player,
            gain,
            sourceCard
        );
        if (rank >= 8 && count % 2 == 0)
        {
            await CardPileCmd.Draw(choiceContext, 1, player);
        }
    }
}

/// <summary>似水流年伴生能力：每回合第一次岁满生成一张年流。</summary>
[RegisterPower]
public sealed class SiShuiLiuNianPower : ModPowerTemplate
{

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://GuZhenRen/images/power/SiShuiLiuNianPower-64x64.png",
        BigIconPath: "res://GuZhenRen/images/power/SiShuiLiuNianPower-256x256.png"
    );
    private static readonly SavedAttachedState<PowerModel, int>
        RankState = new(Entry.ModId + ".zhou_dao.sishui.rank", static () => 8);
    private static readonly SavedAttachedState<PowerModel, int>
        LastTriggerTurnState = new(
            Entry.ModId + ".zhou_dao.sishui.last_turn",
            static () => 0
        );

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    internal int Rank => Math.Clamp(RankState[this], 8, 9);
    internal void SetRank(int rank) => RankState[this] = Math.Clamp(rank, 8, 9);

    internal async Task OnSuiManAsync(PlayerChoiceContext choiceContext)
    {
        if (Owner.Player is not { } player ||
            player.PlayerCombatState is not { } state ||
            LastTriggerTurnState[this] == state.TurnNumber)
        {
            return;
        }

        LastTriggerTurnState[this] = state.TurnNumber;
        AbstractGuZhenRenCard token = Rank >= 9
            ? GuGeneratedCardFactory.Create<NianLiuPlus>(player, 9)
            : GuGeneratedCardFactory.Create<NianLiu>(player, 8);
        await GuGeneratedCardFactory.AddToHandOrDiscard(token, player);
    }
}

/// <summary>三更：剩余若干次“获得年华事件”额外+1年华。</summary>
[RegisterPower]
public sealed class SanGengPower : ModPowerTemplate
{

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://GuZhenRen/images/power/SanGengPower-64x64.png",
        BigIconPath: "res://GuZhenRen/images/power/SanGengPower-256x256.png"
    );
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
}

/// <summary>月蛊安排在下一回合开始兑现的年华。</summary>
[RegisterPower]
public sealed class YueGuDelayPower : ModPowerTemplate
{

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://GuZhenRen/images/power/YueGuDelayPower-64x64.png",
        BigIconPath: "res://GuZhenRen/images/power/YueGuDelayPower-256x256.png"
    );
    private static readonly SavedAttachedState<PowerModel, int>
        SecondPayoutState = new(
            Entry.ModId + ".zhou_dao.yue_gu.second_payout",
            static () => 0
        );

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override bool IsVisibleInternal => false;

    internal static async Task ScheduleAsync(
        PlayerChoiceContext choiceContext,
        Player player,
        int nextTurn,
        int followingTurn,
        CardModel sourceCard
    )
    {
        if (nextTurn <= 0 && followingTurn <= 0)
        {
            return;
        }

        YueGuDelayPower? power = player.Creature.GetPower<YueGuDelayPower>();
        if (power == null)
        {
            power = await PowerCmd.Apply<YueGuDelayPower>(
                choiceContext,
                player.Creature,
                Math.Max(0, nextTurn),
                player.Creature,
                sourceCard
            );
        }
        else if (nextTurn > 0)
        {
            await PowerCmd.ModifyAmount(
                choiceContext,
                power,
                nextTurn,
                player.Creature,
                sourceCard
            );
        }

        if (power != null && followingTurn > 0)
        {
            SecondPayoutState[power] += followingTurn;
        }
    }

    public override async Task AfterEnergyReset(Player player)
    {
        if (player != Owner.Player)
        {
            return;
        }

        int first = Math.Max(0, Amount);
        int second = Math.Max(0, SecondPayoutState[this]);
        await PowerCmd.Remove(this);

        if (first > 0)
        {
            await ZhouDaoPowerSystem.GainNianHua(
                new ThrowingPlayerChoiceContext(),
                player,
                first,
                sourceCard: null
            );
        }

        if (second > 0)
        {
            await PowerCmd.Apply<YueGuDelayPower>(
                new ThrowingPlayerChoiceContext(),
                player.Creature,
                second,
                player.Creature,
                cardSource: null,
                silent: true
            );
        }
    }
}

/// <summary>
/// 缓步：以一次 AttackCommand 为单位共享固定减伤额度；多段攻击不会
/// 每段重复获得完整减伤。
/// </summary>
[RegisterPower]
public sealed class HuanBuPower : ModPowerTemplate
{

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://GuZhenRen/images/power/HuanBuPower-64x64.png",
        BigIconPath: "res://GuZhenRen/images/power/HuanBuPower-256x256.png"
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

/// <summary>隐藏监听器：打出昔影后获得对应年华。</summary>
[RegisterPower]
public sealed class XiYingWatcherPower : ModPowerTemplate
{    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    protected override bool IsVisibleInternal => false;

    public override async Task AfterCardPlayed(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        if (!ReferenceEquals(cardPlay.Player.Creature, Owner) ||
            !ZhouDaoCardState.IsXiYing(cardPlay.Card))
        {
            return;
        }

        await ZhouDaoPowerSystem.GainNianHua(
            choiceContext,
            cardPlay.Player,
            ZhouDaoCardState.GetXiYingNianHua(cardPlay.Card),
            cardPlay.Card
        );
    }
}
