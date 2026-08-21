// ============================================================================
// 中文维护说明
// 文件职责：实现地灾注入战斗循环的状态牌；对应本地化名称“雪月”。
// 主要类型：XueYueEarthCalamity。
// 实现要点：灾劫能力按所实现的细粒度接口由事件路由选择性分派。
// 实现补充：战斗衍生牌必须由当前 CombatState 创建，确保网络卡号和牌堆归属有效。
// 维护约定：灾劫选择先持久化再应用；新增钩子时同步更新事件路由和幂等标记。
// ============================================================================
using GuZhenRen.Cards;
using GuZhenRen.Tribulations.Contracts;
using GuZhenRen.Tribulations.Core;
using GuZhenRen.Tribulations.Runtime;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRen.Tribulations.EarthCalamities.XueYue;

public sealed class XueYueEarthCalamity :
    ITribulationDefinition,
    ITribulationCombatLifecycle,
    ITribulationTurnLifecycle,
    ITribulationGuObserver
{
    private static readonly string Cold = Key("accumulated_cold");
    private static readonly string Remaining = Key("remaining_turns");
    private static readonly string Active = Key("corruption_active");
    private static readonly string Respawn = Key("respawn_next_turn");

    public string Id => TribulationIds.XueYue;
    public TribulationTier Tier => TribulationTier.EarthCalamity;
    public TribulationDanger Danger => TribulationDanger.Aberrant;
    public int BaseWeight => 1;

    public bool CanAppear(in TribulationSelectionContext context) => true;
    public float GetEnemyCompatibilityMultiplier(in TribulationSelectionContext context) => 1f;

    public async Task OnAppliedAsync(TribulationContext context)
    {
        await EarthCalamitySupport.ApplyAnchorPowerAsync<XueYuePower>(context);
        if (!TribulationStateStore.GetFlag(context, Active))
            await InsertCorruptionAsync(context);
    }

    public async Task OnPlayerTurnStartAsync(TribulationContext context, int turn)
    {
        if (TribulationStateStore.GetFlag(context, Respawn))
        {
            TribulationStateStore.SetFlag(context, Respawn, false);
            await InsertCorruptionAsync(context);
        }
    }

    public async Task OnPlayerTurnEndAsync(TribulationContext context, int turn)
    {
        if (!TribulationStateStore.GetFlag(context, Active))
            return;

        int remaining = TribulationStateStore.AddCounter(context, Remaining, -1, 0, 3);
        if (remaining > 0)
            return;

        XueFengJieHenGuCorruptionCard? card = FindCorruption(context);
        if (card != null)
            await CardPileCmd.RemoveFromCombat(card, skipVisuals: false);

        int cold = TribulationStateStore.GetCounter(context, Cold);
        await EarthCalamitySupport.AddStatusToDiscardAsync<HanYueStatusCard>(
            context,
            cold >= 3 ? 2 : 1);
        TribulationStateStore.SetCounter(context, Cold, Math.Min(4, cold + 1));
        TribulationStateStore.SetFlag(context, Active, false);
        TribulationStateStore.SetFlag(context, Respawn, true);
        EarthCalamitySupport.RefreshAnchorPower<XueYuePower>(context);
        await GuCardPileSystem.RefillGuHandAsync(context.Player);
    }

    public async Task OnCombatEndAsync(TribulationContext context)
    {
        XueFengJieHenGuCorruptionCard? card = FindCorruption(context);
        if (card != null)
            await CardPileCmd.RemoveFromCombat(card, skipVisuals: true);
    }

    private static async Task InsertCorruptionAsync(TribulationContext context)
    {
        if (FindCorruption(context) != null)
        {
            TribulationStateStore.SetFlag(context, Active, true);
            return;
        }

        CardModel card = context.Combat.CreateCard(
            ModelDb.Card<XueFengJieHenGuCorruptionCard>(),
            context.Player);
        await CardPileCmd.AddGeneratedCardToCombat(
            card,
            GuCardPileSystem.PileType,
            context.Player,
            CardPilePosition.Top);
        await GuCardPileSystem.RefillGuHandAsync(context.Player);
        int cold = TribulationStateStore.GetCounter(context, Cold);
        TribulationStateStore.SetCounter(context, Remaining, cold >= 2 ? 3 : 2);
        TribulationStateStore.SetFlag(context, Active, true);
    }

    private static XueFengJieHenGuCorruptionCard? FindCorruption(
        TribulationContext context)
    {
        PileType[] piles =
        [
            PileType.Draw,
            PileType.Discard,
            PileType.Hand,
            PileType.Exhaust,
            GuCardPileSystem.PileType,
            GuCardPileSystem.StoragePileType,
            GuCardPileSystem.RecoveryPileType,
        ];
        return piles
            .SelectMany(pile => pile.GetPile(context.Player).Cards)
            .OfType<XueFengJieHenGuCorruptionCard>()
            .FirstOrDefault();
    }

    private static string Key(string local) =>
        TribulationStateStore.Key(TribulationIds.XueYue, local);
}
