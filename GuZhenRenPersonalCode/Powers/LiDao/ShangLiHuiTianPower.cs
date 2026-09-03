using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Powers.LiDao;

/// <summary>
/// 伤力回天的战斗状态。
///
/// 从杀招结算完成后开始，按 CardPlay 统计每张后续牌完整结算期间
/// 对敌人造成的实际生命伤害总和，并只保存其中最高的一次。
/// 苦力加伤已经并入攻击牌第一段，因此会自然计入该牌的伤害统计。
///
/// 本 Power 的回复只能结算一次。濒死时由
/// ShangLiHuiTianTriggerPatch 抢占 ShouldDie，并在
/// AfterPreventingDeath 中执行回复；如果整场没有濒死，则在
/// Hook.AfterCombatEnd 中结算。
/// </summary>
[RegisterPower]
public sealed class ShangLiHuiTianPower : ModPowerTemplate
{
    private sealed class PlayDamageState
    {
        public required CardModel Card { get; init; }
        public int Damage { get; set; }
    }

    private const string RankVar = "Rank";
    private const string MaxDamageVar = "MaxDamage";

    private readonly Dictionary<int, PlayDamageState> _activePlays = [];
    private bool _triggered;
    private bool _deathPreventionClaimed;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override int DisplayAmount => MaxRecordedDamage;

    public int Rank => DynamicVars[RankVar].IntValue;

    public int MaxRecordedDamage =>
        Math.Max(0, DynamicVars[MaxDamageVar].IntValue);

    public int RecoveryPercent => Rank switch
    {
        <= 3 => 40,
        4 => 50,
        5 => 60,
        6 => 75,
        _ => 90,
    };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar(RankVar, 3m),
        new DynamicVar(MaxDamageVar, 0m),
    ];

    public override PowerAssetProfile AssetProfile => new(
        IconPath:
            "res://GuZhenRenPersonal/images/power/ShangLiHuiTianPower-64x64.png",
        BigIconPath:
            "res://GuZhenRenPersonal/images/power/ShangLiHuiTianPower-256x256.png"
    );

    internal void Arm(int rank)
    {
        DynamicVars[RankVar].BaseValue = Math.Clamp(rank, 3, 7);
        DynamicVars[MaxDamageVar].BaseValue = 0m;
        _activePlays.Clear();
        _triggered = false;
        _deathPreventionClaimed = false;
        InvokeDisplayAmountChanged();
    }

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (_triggered ||
            cardPlay.Player.Creature != Owner ||
            !cardPlay.IsFirstInSeries)
        {
            return Task.CompletedTask;
        }

        _activePlays[cardPlay.PlayIndex] = new PlayDamageState
        {
            Card = cardPlay.Card,
            Damage = 0,
        };

        return Task.CompletedTask;
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
        if (_triggered ||
            cardSource == null ||
            cardSource.Owner.Creature != Owner ||
            target.Side == Owner.Side ||
            result.UnblockedDamage <= 0)
        {
            return Task.CompletedTask;
        }

        int playIndex = cardSource.CurrentPlayIndex;
        if (!_activePlays.TryGetValue(
                playIndex,
                out PlayDamageState? state) ||
            !ReferenceEquals(state.Card, cardSource))
        {
            return Task.CompletedTask;
        }

        state.Damage += result.UnblockedDamage;
        return Task.CompletedTask;
    }

    public override Task AfterCardPlayedLate(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        if (_triggered ||
            cardPlay.Player.Creature != Owner ||
            !cardPlay.IsLastInSeries ||
            !_activePlays.Remove(
                cardPlay.PlayIndex,
                out PlayDamageState? state))
        {
            return Task.CompletedTask;
        }

        if (state.Damage > MaxRecordedDamage)
        {
            DynamicVars[MaxDamageVar].BaseValue = state.Damage;
            InvokeDisplayAmountChanged();

            Entry.Logger.Info(
                $"[伤力回天] 刷新后续用牌伤害最高值：" +
                $"{state.Damage}，card={state.Card.Id}。"
            );
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 仅在原版判定“确实会死亡”且没有其他效果先阻止死亡时调用。
    /// 一旦抢占成功，立即锁定本次回复，避免同一死亡流程重复触发。
    /// </summary>
    internal bool TryClaimDeathPrevention()
    {
        if (_triggered ||
            _deathPreventionClaimed ||
            MaxRecordedDamage <= 0)
        {
            return false;
        }

        _deathPreventionClaimed = true;
        return true;
    }

    internal async Task TriggerRecoveryAsync(string reason)
    {
        if (_triggered)
        {
            return;
        }

        _triggered = true;
        _deathPreventionClaimed = true;
        _activePlays.Clear();

        int recordedDamage = MaxRecordedDamage;
        int healing = recordedDamage * RecoveryPercent / 100;
        Entry.Logger.Info(
            $"[伤力回天] 触发回复 reason={reason} recorded={recordedDamage} " +
                $"percent={RecoveryPercent}% healing={healing} " +
            $"hp={Owner.CurrentHp}/{Owner.MaxHp}。"
        );

        if (healing > 0)
        {
            Flash();
            await CreatureCmd.Heal(Owner, healing);
        }

        // 濒死触发后必须立刻移除，确保战斗结束不再二次回复。
        // 战斗结束触发时也统一移除，保持“一次性”语义。
        await PowerCmd.Remove(this);
    }
}
