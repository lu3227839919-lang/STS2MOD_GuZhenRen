// ============================================================================
// 中文维护说明
// 文件职责：实现地灾注入战斗循环的状态牌；对应本地化名称“流星火雨”。
// 主要类型：LiuXingHuoYuEarthCalamity。
// 实现要点：灾劫能力按所实现的细粒度接口由事件路由选择性分派。
// 维护约定：灾劫选择先持久化再应用；新增钩子时同步更新事件路由和幂等标记。
// ============================================================================
using GuZhenRen.Tribulations.Contracts;
using GuZhenRen.Tribulations.Core;
using GuZhenRen.Tribulations.Runtime;
using MegaCrit.Sts2.Core.Entities.Cards;

namespace GuZhenRen.Tribulations.EarthCalamities.LiuXingHuoYu;

public sealed class LiuXingHuoYuEarthCalamity :
    ITribulationDefinition,
    ITribulationCombatLifecycle,
    ITribulationTurnLifecycle,
    ITribulationCardObserver
{
    private static readonly string Cards = Key("cards_this_turn");
    private static readonly string Triggers = Key("triggers_this_turn");
    private static readonly string Intensity = Key("fire_intensity");
    private static readonly string YunJinMade = Key("yun_jin_made_this_turn");

    public string Id => TribulationIds.LiuXingHuoYu;
    public TribulationTier Tier => TribulationTier.EarthCalamity;
    public TribulationDanger Danger => TribulationDanger.Dangerous;
    public int BaseWeight => 1;

    public bool CanAppear(in TribulationSelectionContext context) => true;
    public float GetEnemyCompatibilityMultiplier(in TribulationSelectionContext context) => 1f;

    public Task OnAppliedAsync(TribulationContext context) =>
        EarthCalamitySupport.ApplyAnchorPowerAsync<LiuXingHuoYuPower>(context);

    public async Task OnCardPlayedAsync(
        TribulationContext context,
        CardPlay cardPlay)
    {
        if (!cardPlay.IsFirstInSeries)
            return;

        int count = TribulationStateStore.AddCounter(context, Cards, 1, 0, 999);
        int baseDamage = count switch
        {
            4 => 6,
            7 => 10,
            10 => 14,
            > 10 when (count - 10) % 3 == 0 => 14,
            _ => 0,
        };
        if (baseDamage == 0)
            return;

        int intensity = TribulationStateStore.GetCounter(context, Intensity);
        await EarthCalamitySupport.DamagePlayerAsync(
            context,
            EarthCalamitySupport.ScaleFlat(context, baseDamage + intensity * 2));
        TribulationStateStore.AddCounter(context, Triggers, 1, 0, 99);

        if (intensity >= 3 &&
            !TribulationStateStore.GetFlag(context, YunJinMade))
        {
            TribulationStateStore.SetFlag(context, YunJinMade, true);
            await EarthCalamitySupport.AddStatusToDiscardAsync<YunJinStatusCard>(
                context,
                2);
        }
    }

    public Task OnPlayerTurnEndAsync(TribulationContext context, int turn)
    {
        int triggers = TribulationStateStore.GetCounter(context, Triggers);
        if (triggers >= 2)
            TribulationStateStore.AddCounter(context, Intensity, 1, 0, 3);
        else if (triggers == 0)
            TribulationStateStore.AddCounter(context, Intensity, -1, 0, 3);

        TribulationStateStore.SetCounter(context, Cards, 0);
        TribulationStateStore.SetCounter(context, Triggers, 0);
        TribulationStateStore.SetFlag(context, YunJinMade, false);
        EarthCalamitySupport.RefreshAnchorPower<LiuXingHuoYuPower>(context);
        return Task.CompletedTask;
    }

    private static string Key(string local) =>
        TribulationStateStore.Key(TribulationIds.LiuXingHuoYu, local);
}
