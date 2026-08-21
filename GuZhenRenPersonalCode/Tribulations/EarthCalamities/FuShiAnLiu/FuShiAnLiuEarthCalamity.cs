// ============================================================================
// 中文维护说明
// 文件职责：定义一项地灾的出现条件、危险度与战斗生命周期；对应本地化名称“腐蚀暗流”。
// 主要类型：FuShiAnLiuEarthCalamity。
// 实现要点：灾劫能力按所实现的细粒度接口由事件路由选择性分派。
// 实现补充：战斗变更通过命令队列并等待完成，不直接绕过游戏同步层修改结果。
// 维护约定：灾劫选择先持久化再应用；新增钩子时同步更新事件路由和幂等标记。
// ============================================================================
using GuZhenRen.Tribulations.Contracts;
using GuZhenRen.Tribulations.Core;
using GuZhenRen.Tribulations.Runtime;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace GuZhenRen.Tribulations.EarthCalamities.FuShiAnLiu;

public sealed class FuShiAnLiuEarthCalamity :
    ITribulationDefinition,
    ITribulationCombatLifecycle,
    ITribulationTurnLifecycle,
    ITribulationDamageObserver,
    ITribulationCombatModifier
{
    private static readonly string RawBlock = Key("raw_block_this_turn");
    private static readonly string CurrentRawBlock = Key("current_raw_block");
    private static readonly string Breach = Key("breach");
    private static readonly string EnemyTurn = Key("enemy_turn_active");
    private static readonly string HpLost = Key("hp_lost_enemy_turn");
    private static readonly string FirstBlockBoost = Key("first_block_boost");
    private static readonly string ApplyingFirstBlockBoost =
        Key("applying_first_block_boost");

    public string Id => TribulationIds.FuShiAnLiu;
    public TribulationTier Tier => TribulationTier.EarthCalamity;
    public TribulationDanger Danger => TribulationDanger.Dangerous;
    public int BaseWeight => 1;

    public bool CanAppear(in TribulationSelectionContext context) => true;
    public float GetEnemyCompatibilityMultiplier(in TribulationSelectionContext context) => 1f;

    public Task OnAppliedAsync(TribulationContext context) =>
        EarthCalamitySupport.ApplyAnchorPowerAsync<FuShiAnLiuPower>(context);

    public Task OnPlayerTurnStartAsync(TribulationContext context, int turn)
    {
        TribulationStateStore.SetDecimal(context, RawBlock, 0m);
        TribulationStateStore.SetDecimal(context, CurrentRawBlock, 0m);
        TribulationStateStore.SetFlag(context, ApplyingFirstBlockBoost, false);
        return Task.CompletedTask;
    }

    public Task OnEnemyTurnStartAsync(TribulationContext context, int round)
    {
        TribulationStateStore.SetFlag(context, EnemyTurn, true);
        TribulationStateStore.SetFlag(context, HpLost, false);
        return Task.CompletedTask;
    }

    public Task OnPlayerHpDamageTakenAsync(TribulationContext context, decimal amount)
    {
        if (TribulationStateStore.GetFlag(context, EnemyTurn))
            TribulationStateStore.SetFlag(context, HpLost, true);
        return Task.CompletedTask;
    }

    public async Task OnEnemyTurnEndAsync(TribulationContext context, int round)
    {
        bool lost = TribulationStateStore.GetFlag(context, HpLost);
        int breach = TribulationStateStore.GetCounter(context, Breach);
        if (lost && breach >= 3)
        {
            decimal block = context.Player.Creature.Block;
            if (block > 0m)
            {
                await CreatureCmd.LoseBlock(
                    new ThrowingPlayerChoiceContext(),
                    context.Player.Creature,
                    block,
                    context.Leader);
            }
            TribulationStateStore.SetFlag(context, FirstBlockBoost, true);
        }
        else if (lost)
        {
            TribulationStateStore.SetCounter(context, Breach, breach + 1);
        }
        else
        {
            TribulationStateStore.AddCounter(context, Breach, -1, 0, 3);
        }

        TribulationStateStore.SetFlag(context, EnemyTurn, false);
        TribulationStateStore.SetFlag(context, HpLost, false);
        EarthCalamitySupport.RefreshAnchorPower<FuShiAnLiuPower>(context);
    }

    public Task OnBeforeBlockGainedAsync(
        TribulationContext context,
        Creature target,
        decimal rawAmount,
        ValueProp props,
        CardModel? cardSource)
    {
        if (ReferenceEquals(target, context.Player.Creature) && rawAmount > 0m)
        {
            bool boost = TribulationStateStore.GetFlag(context, FirstBlockBoost);
            TribulationStateStore.SetFlag(context, ApplyingFirstBlockBoost, boost);
            if (boost)
                TribulationStateStore.SetFlag(context, FirstBlockBoost, false);
            TribulationStateStore.SetDecimal(context, CurrentRawBlock, rawAmount);
            TribulationStateStore.SetDecimal(
                context,
                RawBlock,
                TribulationStateStore.GetDecimal(context, RawBlock) + rawAmount);
        }
        return Task.CompletedTask;
    }

    public decimal ModifyPlayerBlockGain(
        TribulationContext context,
        Creature target,
        decimal amount,
        ValueProp props,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        if (!ReferenceEquals(target, context.Player.Creature) || amount <= 0m)
            return amount;

        decimal rawAmount = TribulationStateStore.GetDecimal(
            context,
            CurrentRawBlock);
        if (rawAmount <= 0m)
            rawAmount = amount;
        decimal total = TribulationStateStore.GetDecimal(context, RawBlock);
        decimal previous = Math.Max(0m, total - rawAmount);
        decimal firstThreshold = Math.Max(
            6m,
            18m - 4m * TribulationStateStore.GetCounter(context, Breach));
        decimal secondThreshold = firstThreshold + 12m;
        decimal result = Segment(previous, rawAmount, 0m, firstThreshold, 1m) +
            Segment(previous, rawAmount, firstThreshold, secondThreshold, 0.50m) +
            Segment(previous, rawAmount, secondThreshold, decimal.MaxValue, 0.25m);

        if (TribulationStateStore.GetFlag(context, ApplyingFirstBlockBoost))
            result *= 0.50m;
        return result;
    }

    public Task OnBlockGainedAsync(
        TribulationContext context,
        Creature target,
        decimal finalAmount,
        ValueProp props,
        CardModel? cardSource)
    {
        if (ReferenceEquals(target, context.Player.Creature))
        {
            TribulationStateStore.SetFlag(context, ApplyingFirstBlockBoost, false);
            TribulationStateStore.SetDecimal(context, CurrentRawBlock, 0m);
        }
        return Task.CompletedTask;
    }

    private static decimal Segment(
        decimal previous,
        decimal amount,
        decimal start,
        decimal end,
        decimal efficiency)
    {
        decimal overlap = Math.Max(
            0m,
            Math.Min(previous + amount, end) - Math.Max(previous, start));
        return overlap * efficiency;
    }

    private static string Key(string local) =>
        TribulationStateStore.Key(TribulationIds.FuShiAnLiu, local);
}
