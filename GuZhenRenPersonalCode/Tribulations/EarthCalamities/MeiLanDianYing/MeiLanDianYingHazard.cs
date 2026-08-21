// ============================================================================
// 中文维护说明
// 文件职责：实现地灾注入战斗循环的状态牌。
// 主要类型：MeiLanDianYingHazard、MeiLanDianYingHazardResult。
// 实现要点：战斗变更通过命令队列并等待完成，不直接绕过游戏同步层修改结果。
// 维护约定：灾劫选择先持久化再应用；新增钩子时同步更新事件路由和幂等标记。
// ============================================================================
using GuZhenRen.Tribulations.Core;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace GuZhenRen.Tribulations.EarthCalamities.MeiLanDianYing;

/// <summary>
/// Runtime-only hazard. It deliberately is not a Creature, so it cannot alter
/// enemy slots, AOE targeting, kill checks, or combat victory conditions.
/// </summary>
internal static class MeiLanDianYingHazard
{
    internal const string StrikeIntentId = "mei_lan_dian_ying/strike";
    internal const string ExecuteIntentId = "mei_lan_dian_ying/execute";
    internal const string FaultIntentId = "mei_lan_dian_ying/fault";

    internal static async Task<MeiLanDianYingHazardResult> ResolveAsync(
        TribulationContext context,
        int hunt,
        bool exploitFault)
    {
        if (exploitFault)
        {
            await EarthCalamitySupport
                .AddStatusToDiscardAsync<FuDiLouDongStatusCard>(context);
            return new MeiLanDianYingHazardResult(
                DealtHpDamage: false,
                HuntAfterAction: hunt,
                IntentId: FaultIntentId);
        }

        int hpBefore = context.Player.Creature.CurrentHp;
        string intentId;
        if (hunt >= 3)
        {
            decimal removed = Math.Ceiling(context.Player.Creature.Block * 0.50m);
            if (removed > 0m)
            {
                await CreatureCmd.LoseBlock(
                    new ThrowingPlayerChoiceContext(),
                    context.Player.Creature,
                    removed,
                    context.Leader);
            }
            await EarthCalamitySupport.DamagePlayerAsync(
                context,
                EarthCalamitySupport.ScaleFlat(context, 18));
            hunt = 1;
            intentId = ExecuteIntentId;
        }
        else
        {
            await EarthCalamitySupport.DamagePlayerAsync(
                context,
                EarthCalamitySupport.ScaleFlat(context, 6 + hunt * 4));
            intentId = StrikeIntentId;
        }

        return new MeiLanDianYingHazardResult(
            context.Player.Creature.CurrentHp < hpBefore,
            hunt,
            intentId);
    }
}

internal readonly record struct MeiLanDianYingHazardResult(
    bool DealtHpDamage,
    int HuntAfterAction,
    string IntentId);
