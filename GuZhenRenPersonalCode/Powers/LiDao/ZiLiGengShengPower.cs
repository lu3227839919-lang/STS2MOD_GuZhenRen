using GuZhenRen.Cards.LiDao;

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
/// 自力更生蛊的待触发状态。只统计标记攻击对主目标造成的实际
/// 生命伤害；格挡与过量伤害不会进入回复基数。
/// </summary>
[RegisterPower]
public sealed class ZiLiGengShengPower : ModPowerTemplate
{
    private bool _armed;
    private bool _trackingAttack;
    private CardModel? _trackedCard;
    private Creature? _primaryTarget;
    private int _actualDamage;
    private bool _killedPrimaryTarget;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override int DisplayAmount => _armed ? 1 : 0;

    public int Rank => DynamicVars["Rank"].IntValue;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("Rank", 3m)];

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://GuZhenRenPersonal/images/power/ZiLiGengShengPower-64x64.png",
        BigIconPath: "res://GuZhenRenPersonal/images/power/ZiLiGengShengPower-256x256.png"
    );

    internal void ConfigureRank(int rank)
    {
        DynamicVars["Rank"].BaseValue = Math.Clamp(rank, 3, 7);
        InvokeDisplayAmountChanged();
    }

    internal void Arm()
    {
        _armed = true;
        ClearTrackedAttack();
        InvokeDisplayAmountChanged();
    }

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (!_armed ||
            _trackingAttack ||
            cardPlay.Player.Creature != Owner ||
            cardPlay.Card.Type != CardType.Attack ||
            !cardPlay.IsFirstInSeries)
        {
            return Task.CompletedTask;
        }

        _armed = false;
        _trackingAttack = true;
        _trackedCard = cardPlay.Card;
        _primaryTarget = GetPrimaryTarget(cardPlay);
        _actualDamage = 0;
        _killedPrimaryTarget = false;
        InvokeDisplayAmountChanged();
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
        if (!_trackingAttack ||
            dealer != Owner ||
            target != _primaryTarget ||
            cardSource != _trackedCard)
        {
            return Task.CompletedTask;
        }

        _actualDamage += result.UnblockedDamage;
        _killedPrimaryTarget |= result.WasTargetKilled;
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayedLate(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        if (!_trackingAttack ||
            !cardPlay.IsLastInSeries ||
            cardPlay.Card != _trackedCard)
        {
            return;
        }

        int actualDamage = _actualDamage;
        bool killedTarget = _killedPrimaryTarget;
        ClearTrackedAttack();

        int healing = Math.Min(
            ZiLiGengShengGu.HealingCapAtRank(Rank, killedTarget),
            (int)Math.Round(
                actualDamage *
                    ZiLiGengShengGu.HealingRatioAtRank(Rank),
                MidpointRounding.AwayFromZero
            )
        );
        if (healing > 0)
        {
            await CreatureCmd.Heal(Owner, healing);
        }

        await PowerCmd.Remove(this);
    }

    private Creature? GetPrimaryTarget(CardPlay cardPlay) =>
        cardPlay.Target ??
        cardPlay.Card.CombatState?
            .GetOpponentsOf(Owner)
            .FirstOrDefault(creature => creature.IsHittable);

    private void ClearTrackedAttack()
    {
        _trackingAttack = false;
        _trackedCard = null;
        _primaryTarget = null;
        _actualDamage = 0;
        _killedPrimaryTarget = false;
    }
}
