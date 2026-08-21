// ============================================================================
// 中文维护说明
// 文件职责：定义一项地灾的出现条件、危险度与战斗生命周期；对应本地化名称“阴云白海”。
// 主要类型：YinYunBaiHaiEarthCalamity。
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
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRen.Tribulations.EarthCalamities.YinYunBaiHai;

public sealed class YinYunBaiHaiEarthCalamity :
    ITribulationDefinition,
    ITribulationCombatLifecycle,
    ITribulationTurnLifecycle,
    ITribulationCombatModifier
{
    private static readonly string AppliedCountPrefix = Key("applied_debuff_count.");
    private static readonly string WashedId = Key("washed_debuff_id");
    private static readonly string WashStreak = Key("wash_streak");
    private static readonly string Assimilation = Key("assimilation_percent");

    public string Id => TribulationIds.YinYunBaiHai;
    public TribulationTier Tier => TribulationTier.EarthCalamity;
    public TribulationDanger Danger => TribulationDanger.Dangerous;
    public int BaseWeight => 1;

    public bool CanAppear(in TribulationSelectionContext context) => true;
    public float GetEnemyCompatibilityMultiplier(in TribulationSelectionContext context) => 1f;

    public Task OnAppliedAsync(TribulationContext context) =>
        EarthCalamitySupport.ApplyAnchorPowerAsync<YinYunBaiHaiPower>(context);

    public Task OnPlayerTurnStartAsync(TribulationContext context, int turn)
    {
        TribulationStateStore.RemovePrefix(context, AppliedCountPrefix);
        return Task.CompletedTask;
    }

    public async Task OnEnemyTurnStartAsync(TribulationContext context, int round)
    {
        PowerModel? highest = context.Leader.Powers
            .Where(power => power.Type == PowerType.Debuff && power.Amount > 0)
            .OrderByDescending(power => power.Amount)
            .ThenBy(power => power.Id.Entry, StringComparer.Ordinal)
            .FirstOrDefault();
        if (highest == null)
        {
            return;
        }

        int washed = Math.Min(
            highest.Amount,
            Math.Max(2, (int)Math.Ceiling(highest.Amount * 0.25m)));
        await PowerCmd.ModifyAmount(
            new ThrowingPlayerChoiceContext(),
            highest,
            -washed,
            context.Leader,
            cardSource: null,
            silent: false);
        await EarthCalamitySupport.GainBlockAsync(
            context.Leader,
            EarthCalamitySupport.ScaleFlat(context, washed * 2));

        string id = highest.Id.ToString();
        string previous = TribulationStateStore.GetText(context, WashedId);
        int streak = string.Equals(previous, id, StringComparison.Ordinal)
            ? TribulationStateStore.AddCounter(context, WashStreak, 1, 0, 99)
            : 1;
        TribulationStateStore.SetText(context, WashedId, id);
        TribulationStateStore.SetCounter(context, WashStreak, streak);
        TribulationStateStore.SetCounter(
            context,
            Assimilation,
            streak >= 3 ? 50 : streak >= 2 ? 75 : 100);
        EarthCalamitySupport.RefreshAnchorPower<YinYunBaiHaiPower>(context);
    }

    public bool TryModifyPowerAmountReceived(
        TribulationContext context,
        PowerModel canonicalPower,
        Creature target,
        decimal amount,
        Creature? applier,
        out decimal modifiedAmount)
    {
        modifiedAmount = amount;
        if (!ReferenceEquals(target, context.Leader) ||
            canonicalPower.Type != PowerType.Debuff ||
            amount <= 0m ||
            applier?.IsPlayer != true)
            return false;

        string id = canonicalPower.Id.ToString();
        string countKey = AppliedCountPrefix + id;
        int count = TribulationStateStore.AddCounter(
            context,
            countKey,
            1,
            1,
            999);

        decimal repeatEfficiency = count switch
        {
            1 => 1m,
            2 => 0.75m,
            3 => 0.50m,
            _ => 0.25m,
        };
        int assimilation = TribulationStateStore.GetCounter(context, Assimilation);
        decimal assimilated = count == 1 && string.Equals(
            TribulationStateStore.GetText(context, WashedId),
            id,
            StringComparison.Ordinal)
                ? (assimilation <= 0 ? 1m : assimilation / 100m)
                : 1m;
        modifiedAmount = amount * repeatEfficiency * assimilated;
        return modifiedAmount != amount;
    }

    private static string Key(string local) =>
        TribulationStateStore.Key(TribulationIds.YinYunBaiHai, local);
}
