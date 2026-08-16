using GuZhenRen.Cards.LiDao;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Powers.LiDao;

/// <summary>
/// 苦力蛊的持续战斗状态。伤势按当前生命比例分为健康、轻伤、
/// 重伤、极重伤四档；追加伤害每张攻击牌只结算一次。
/// </summary>
[RegisterPower]
public sealed class KuLiPower : ModPowerTemplate
{
    private int _lastTriggeredTurn;
    private int _lastActivationTurn;
    private int _boostedTurn;
    private int _enteredInjuryMask;
    private CardModel? _pendingAttackCard;
    private Creature? _pendingPrimaryTarget;
    private bool _pendingShouldTrigger;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override int DisplayAmount => InjuryTier;

    public int Rank => DynamicVars["Rank"].IntValue;

    public int InjuryTier
    {
        get
        {
            int maxHp = Math.Max(1, Owner.MaxHp);
            int weightedHp = Owner.CurrentHp * 100;
            if (weightedHp > maxHp * 75)
            {
                return 0;
            }
            if (weightedHp > maxHp * 50)
            {
                return 1;
            }
            if (weightedHp > maxHp * 25)
            {
                return 2;
            }
            return 3;
        }
    }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("Rank", 3m)];

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://GuZhenRenPersonal/images/power/KuLiPower-64x64.png",
        BigIconPath: "res://GuZhenRenPersonal/images/power/KuLiPower-256x256.png"
    );

    internal void ConfigureRank(int rank)
    {
        DynamicVars["Rank"].BaseValue = Math.Clamp(rank, 3, 7);
        InvokeDisplayAmountChanged();
    }

    internal void RecordActivation()
    {
        int turn = CurrentTurn;
        if (_lastActivationTurn == turn)
        {
            _boostedTurn = turn;
        }
        _lastActivationTurn = turn;
    }

    internal Task SyncInjuryThresholdsAsync(
        PlayerChoiceContext choiceContext
    ) => GrantNewThresholdStrengthAsync(choiceContext);

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Player.Creature != Owner ||
            cardPlay.Card.Type != CardType.Attack ||
            !cardPlay.IsFirstInSeries)
        {
            return Task.CompletedTask;
        }

        int turn = CurrentTurn;
        _pendingAttackCard = cardPlay.Card;
        _pendingPrimaryTarget = GetPrimaryTarget(cardPlay);
        _pendingShouldTrigger = Rank >= 4 || _lastTriggeredTurn != turn;
        if (_pendingShouldTrigger && Rank == 3)
        {
            _lastTriggeredTurn = turn;
        }
        Entry.Logger.Info(
            $"[苦力] BeforeCardPlayed 记录攻击 {cardPlay.Card.Id}，" +
            $"turn={turn} rank={Rank} shouldTrigger={_pendingShouldTrigger} " +
            $"target={_pendingPrimaryTarget?.CombatId ?? 0}。"
        );
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        if (!cardPlay.IsLastInSeries ||
            cardPlay.Card != _pendingAttackCard)
        {
            Entry.Logger.Info(
                $"[苦力] AfterCardPlayed 跳过：card={cardPlay.Card.Id} " +
                $"lastInSeries={cardPlay.IsLastInSeries} " +
                $"pending={(_pendingAttackCard?.Id.ToString() ?? "null")}。"
            );
            return;
        }

        Creature? target = _pendingPrimaryTarget;
        bool shouldTrigger = _pendingShouldTrigger;
        ClearPendingAttack();
        if (!shouldTrigger || target == null || !target.IsAlive)
        {
            Entry.Logger.Info(
                $"[苦力] AfterCardPlayed 放弃：shouldTrigger={shouldTrigger} " +
                $"target={target?.CombatId ?? 0} alive={target?.IsAlive}。"
            );
            return;
        }

        int damage = KuLiGu.ExtraDamageAtRank(Rank, InjuryTier);
        if (damage <= 0)
        {
            Entry.Logger.Info(
                $"[苦力] AfterCardPlayed 伤害为 0：rank={Rank} injury={InjuryTier} " +
                $"hp={Owner.CurrentHp}/{Owner.MaxHp}。"
            );
            return;
        }
        if (_boostedTurn == CurrentTurn)
        {
            damage = (int)Math.Round(
                damage * 1.5m,
                MidpointRounding.AwayFromZero
            );
        }

        Entry.Logger.Info(
            $"[苦力] 结算苦力伤害 damage={damage} rank={Rank} " +
            $"injury={InjuryTier} target={target.CombatId}。"
        );
        ZiLiGengShengPower? selfReliance =
            Owner.GetPower<ZiLiGengShengPower>();
        selfReliance?.BeginAttachedLiDaoDamage(cardPlay.Card);
        try
        {
            await DamageCmd.Attack(damage)
                .FromCard(cardPlay.Card, cardPlay)
                .Targeting(target)
                .Unpowered()
                .Execute(choiceContext);
        }
        finally
        {
            selfReliance?.EndAttachedLiDaoDamage(cardPlay.Card);
        }
    }

    public override async Task AfterCurrentHpChanged(
        Creature creature,
        decimal delta
    )
    {
        if (creature != Owner)
        {
            return;
        }

        InvokeDisplayAmountChanged();
        if (delta < 0m)
        {
            await GrantNewThresholdStrengthAsync(
                new ThrowingPlayerChoiceContext()
            );
        }
    }

    private async Task GrantNewThresholdStrengthAsync(
        PlayerChoiceContext choiceContext
    )
    {
        if (Rank < 6)
        {
            return;
        }

        for (int tier = 1; tier <= InjuryTier; tier++)
        {
            int bit = 1 << tier;
            if ((_enteredInjuryMask & bit) != 0)
            {
                continue;
            }

            _enteredInjuryMask |= bit;
            int strength = KuLiGu.StrengthAtThreshold(Rank, tier);
            if (strength > 0)
            {
                await PowerCmd.Apply<StrengthPower>(
                    choiceContext,
                    Owner,
                    strength,
                    Owner,
                    null
                );
            }
        }
    }

    private Creature? GetPrimaryTarget(CardPlay cardPlay) =>
        cardPlay.Target ??
        cardPlay.Card.CombatState?
            .GetOpponentsOf(Owner)
            .FirstOrDefault(creature => creature.IsHittable);

    private int CurrentTurn =>
        Owner.Player?.PlayerCombatState?.TurnNumber ?? 0;

    private void ClearPendingAttack()
    {
        _pendingAttackCard = null;
        _pendingPrimaryTarget = null;
        _pendingShouldTrigger = false;
    }
}
