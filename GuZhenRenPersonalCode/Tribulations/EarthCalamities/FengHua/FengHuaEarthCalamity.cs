// ============================================================================
// 中文维护说明
// 文件职责：定义一项地灾的出现条件、危险度与战斗生命周期；对应本地化名称“风花劫”。
// 主要类型：FengHuaEarthCalamity。
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

namespace GuZhenRen.Tribulations.EarthCalamities.FengHua;

public sealed class FengHuaEarthCalamity :
    ITribulationDefinition,
    ITribulationCombatLifecycle,
    ITribulationTurnLifecycle,
    ITribulationDamageObserver,
    ITribulationCombatModifier
{
    private static readonly string WindEye = Key("wind_eye");
    private static readonly string AllAttackBonus = Key("all_attack_bonus");
    private static readonly string FirstAttackUsedPrefix = Key("first_attack_used.");

    public string Id => TribulationIds.FengHua;
    public TribulationTier Tier => TribulationTier.EarthCalamity;
    public TribulationDanger Danger => TribulationDanger.Common;
    public int BaseWeight => 1;

    public bool CanAppear(in TribulationSelectionContext context) => true;
    public float GetEnemyCompatibilityMultiplier(in TribulationSelectionContext context) => 1f;

    public Task OnAppliedAsync(TribulationContext context) =>
        EarthCalamitySupport.ApplyAnchorPowerAsync<FengHuaPower>(context);

    public async Task OnEnemyTurnStartAsync(TribulationContext context, int round)
    {
        TribulationStateStore.SetFlag(context, AllAttackBonus, false);
        TribulationStateStore.RemovePrefix(context, FirstAttackUsedPrefix);
        int eye = TribulationStateStore.GetCounter(context, WindEye);
        if (context.Player.Creature.Block <= 5m && eye > 0)
        {
            eye = TribulationStateStore.AddCounter(context, WindEye, -1, 0, 3);
        }

        if (context.Leader.Monster?.IntendsToAttack != true)
        {
            EarthCalamitySupport.RefreshAnchorPower<FengHuaPower>(context);
            return;
        }

        decimal currentBlock = context.Player.Creature.Block;
        decimal desired = Math.Clamp(
            Math.Ceiling(currentBlock * 0.35m),
            EarthCalamitySupport.ScaleFlat(context, 5),
            EarthCalamitySupport.ScaleFlat(context, 15)) +
            eye * EarthCalamitySupport.ScaleFlat(context, 2);
        decimal removed = Math.Min(currentBlock, desired);
        if (removed > 0m)
        {
            await CreatureCmd.LoseBlock(
                new ThrowingPlayerChoiceContext(),
                context.Player.Creature,
                removed,
                context.Leader);
            await EarthCalamitySupport.GainBlockAsync(
                context.Leader,
                Math.Min(
                    EarthCalamitySupport.ScaleFlat(context, 10),
                    Math.Floor(removed * 0.50m)));
        }

        if (removed >= 10m)
        {
            if (eye >= 3)
            {
                TribulationStateStore.SetCounter(context, WindEye, 1);
                TribulationStateStore.SetFlag(context, AllAttackBonus, true);
            }
            else
            {
                TribulationStateStore.SetCounter(context, WindEye, eye + 1);
            }
        }
        EarthCalamitySupport.RefreshAnchorPower<FengHuaPower>(context);
    }

    public Task OnEnemyTurnEndAsync(TribulationContext context, int round)
    {
        TribulationStateStore.SetFlag(context, AllAttackBonus, false);
        return Task.CompletedTask;
    }

    public decimal ModifyDamageAdditive(
        TribulationContext context,
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        if (target?.IsPlayer != true ||
            dealer?.IsEnemy != true ||
            !props.IsPoweredAttack() ||
            !TribulationStateStore.GetFlag(context, AllAttackBonus))
            return 0m;

        string usedKey = FirstAttackUsedPrefix + dealer.CombatId;
        if (TribulationStateStore.GetFlag(context, usedKey))
            return 0m;
        return EarthCalamitySupport.ScaleFlat(context, 4);
    }

    public Task OnDamageResolvedAsync(
        TribulationContext context,
        TribulationDamageEvent damage)
    {
        if (damage.Dealer?.IsEnemy == true &&
            damage.Target.IsPlayer &&
            damage.IsAttack &&
            TribulationStateStore.GetFlag(context, AllAttackBonus))
        {
            TribulationStateStore.SetFlag(
                context,
                FirstAttackUsedPrefix + damage.Dealer.CombatId,
                true);
        }
        return Task.CompletedTask;
    }

    private static string Key(string local) =>
        TribulationStateStore.Key(TribulationIds.FengHua, local);
}
