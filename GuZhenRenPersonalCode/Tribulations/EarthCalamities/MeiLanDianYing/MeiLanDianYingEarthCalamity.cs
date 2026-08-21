// ============================================================================
// 中文维护说明
// 文件职责：定义一项地灾的出现条件、危险度与战斗生命周期；对应本地化名称“魅蓝电影”。
// 主要类型：MeiLanDianYingEarthCalamity。
// 实现要点：灾劫能力按所实现的细粒度接口由事件路由选择性分派。
// 维护约定：灾劫选择先持久化再应用；新增钩子时同步更新事件路由和幂等标记。
// ============================================================================
using GuZhenRen.Tribulations.Contracts;
using GuZhenRen.Tribulations.Core;
using GuZhenRen.Tribulations.Runtime;

namespace GuZhenRen.Tribulations.EarthCalamities.MeiLanDianYing;

public sealed class MeiLanDianYingEarthCalamity :
    ITribulationDefinition,
    ITribulationCombatLifecycle,
    ITribulationTurnLifecycle,
    ITribulationDamageObserver
{
    private static readonly string Hunt = Key("hunt");
    private static readonly string EnemyTurn = Key("enemy_turn_active");
    private static readonly string RegularHpDamage = Key("regular_enemy_hp_damage");
    private static readonly string Misses = Key("hazard_miss_streak");

    public string Id => TribulationIds.MeiLanDianYing;
    public TribulationTier Tier => TribulationTier.EarthCalamity;
    public TribulationDanger Danger => TribulationDanger.Aberrant;
    public int BaseWeight => 1;

    public bool CanAppear(in TribulationSelectionContext context) => true;
    public float GetEnemyCompatibilityMultiplier(in TribulationSelectionContext context) => 1f;

    public Task OnAppliedAsync(TribulationContext context) =>
        EarthCalamitySupport.ApplyAnchorPowerAsync<MeiLanDianYingPower>(context);

    public Task OnEnemyTurnStartAsync(TribulationContext context, int round)
    {
        TribulationStateStore.SetFlag(context, EnemyTurn, true);
        TribulationStateStore.SetFlag(context, RegularHpDamage, false);
        return Task.CompletedTask;
    }

    public Task OnDamageResolvedAsync(
        TribulationContext context,
        TribulationDamageEvent damage)
    {
        if (TribulationStateStore.GetFlag(context, EnemyTurn) &&
            damage.Target.IsPlayer &&
            damage.Dealer?.IsEnemy == true &&
            damage.IsAttack &&
            damage.HpDamage > 0m)
        {
            TribulationStateStore.SetFlag(context, RegularHpDamage, true);
        }
        return Task.CompletedTask;
    }

    public async Task OnEnemyTurnEndAsync(TribulationContext context, int round)
    {
        bool hunted = TribulationStateStore.GetFlag(context, RegularHpDamage);
        int hunt = TribulationStateStore.GetCounter(context, Hunt);
        if (hunted)
            hunt = Math.Min(3, hunt + 1);
        TribulationStateStore.SetCounter(context, Hunt, hunt);
        TribulationStateStore.SetFlag(context, EnemyTurn, false);

        int misses = TribulationStateStore.GetCounter(context, Misses);
        bool exploitFault = misses >= 2;
        MeiLanDianYingHazardResult result =
            await MeiLanDianYingHazard.ResolveAsync(
                context,
                hunt,
                exploitFault);
        TribulationStateStore.SetCounter(context, Hunt, result.HuntAfterAction);
        TribulationStateStore.SetCounter(
            context,
            Misses,
            exploitFault || result.DealtHpDamage
                ? 0
                : Math.Min(2, misses + 1));
        EarthCalamitySupport.RefreshAnchorPower<MeiLanDianYingPower>(context);
    }

    private static string Key(string local) =>
        TribulationStateStore.Key(TribulationIds.MeiLanDianYing, local);
}
