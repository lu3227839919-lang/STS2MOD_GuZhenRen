// ============================================================================
// 中文维护说明
// 文件职责：定义一项地灾的出现条件、危险度与战斗生命周期；对应本地化名称“熔地”。
// 主要类型：RongDiEarthCalamity。
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

namespace GuZhenRen.Tribulations.EarthCalamities.RongDi;

public sealed class RongDiEarthCalamity :
    ITribulationDefinition,
    ITribulationCombatLifecycle,
    ITribulationTurnLifecycle,
    ITribulationResourceObserver,
    ITribulationDamageObserver,
    ITribulationCombatModifier
{
    private static readonly string Spent = Key("spent_this_turn");
    private static readonly string Overheat = Key("overheat");
    private static readonly string Erupted = Key("erupted_this_turn");
    private static readonly string NextAttack = Key("next_attack_bonus");

    public string Id => TribulationIds.RongDi;
    public TribulationTier Tier => TribulationTier.EarthCalamity;
    public TribulationDanger Danger => TribulationDanger.Common;
    public int BaseWeight => 1;

    public bool CanAppear(in TribulationSelectionContext context) => true;
    public float GetEnemyCompatibilityMultiplier(in TribulationSelectionContext context) => 1f;

    public Task OnAppliedAsync(TribulationContext context) =>
        EarthCalamitySupport.ApplyAnchorPowerAsync<RongDiPower>(context);

    public async Task OnYuanQiSpentAsync(TribulationContext context, int amount)
    {
        for (int i = 0; i < amount; i++)
        {
            int point = TribulationStateStore.AddCounter(context, Spent, 1, 0, 99);
            int fire = point switch
            {
                3 => 4,
                4 => 7,
                >= 5 => 7,
                _ => 0,
            };
            if (fire > 0)
            {
                fire += 2 * TribulationStateStore.GetCounter(context, Overheat);
                await EarthCalamitySupport.DamagePlayerAsync(
                    context,
                    EarthCalamitySupport.ScaleFlat(context, fire));
            }

            if (point == 4 &&
                TribulationStateStore.GetCounter(context, Overheat) >= 3 &&
                !TribulationStateStore.GetFlag(context, Erupted))
            {
                TribulationStateStore.SetFlag(context, Erupted, true);
                decimal removed = Math.Ceiling(context.Player.Creature.Block * 0.50m);
                if (removed > 0m)
                {
                    await CreatureCmd.LoseBlock(
                        new ThrowingPlayerChoiceContext(),
                        context.Player.Creature,
                        removed,
                        context.Leader);
                }
                TribulationStateStore.SetFlag(context, NextAttack, true);
            }
        }
    }

    public async Task OnPlayerTurnEndAsync(TribulationContext context, int turn)
    {
        int spent = TribulationStateStore.GetCounter(context, Spent);
        if (spent == 0)
        {
            await EarthCalamitySupport.GainBlockAsync(
                context.Leader,
                EarthCalamitySupport.ScaleFlat(context, 15));
        }
        else if (spent >= 4)
        {
            TribulationStateStore.AddCounter(context, Overheat, 1, 0, 3);
        }
        else if (spent <= 2)
        {
            TribulationStateStore.AddCounter(context, Overheat, -1, 0, 3);
        }

        TribulationStateStore.SetCounter(context, Spent, 0);
        TribulationStateStore.SetFlag(context, Erupted, false);
        EarthCalamitySupport.RefreshAnchorPower<RongDiPower>(context);
    }

    public Task OnEnemyTurnEndAsync(TribulationContext context, int round)
    {
        TribulationStateStore.SetFlag(context, NextAttack, false);
        return Task.CompletedTask;
    }

    public Task OnDamageResolvedAsync(
        TribulationContext context,
        TribulationDamageEvent damage)
    {
        if (ReferenceEquals(damage.Dealer, context.Leader) &&
            damage.IsAttack)
        {
            TribulationStateStore.SetFlag(context, NextAttack, false);
        }
        return Task.CompletedTask;
    }

    public decimal ModifyDamageAdditive(
        TribulationContext context,
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay) =>
        ReferenceEquals(dealer, context.Leader) &&
        props.IsPoweredAttack() &&
        TribulationStateStore.GetFlag(context, NextAttack)
            ? EarthCalamitySupport.ScaleFlat(context, 10)
            : 0m;

    private static string Key(string local) =>
        TribulationStateStore.Key(TribulationIds.RongDi, local);
}
