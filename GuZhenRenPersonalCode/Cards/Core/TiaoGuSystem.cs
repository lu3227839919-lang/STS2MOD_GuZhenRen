using System.Runtime.CompilerServices;

using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRen.Cards;

/// <summary>
/// 调蛊核心规则：把蛊手牌中的一只常规蛊送入蛊存放 FIFO 队尾，
/// 随后通过统一补位流程把当前队首送入蛊手牌。
///
/// 本系统不绑定任何输入方式。后续蛊虫效果应在同步行动中调用
/// <see cref="TuneGuAsync"/>，或通过 <see cref="Service"/> 依赖服务接口。
/// </summary>
public sealed class TiaoGuSystem : ITiaoGuService
{
    private static readonly ConditionalWeakTable<Player, SemaphoreSlim>
        OperationGates = new();

    private TiaoGuSystem()
    {
    }

    /// <summary>
    /// 供后续蛊虫效果依赖的统一调蛊服务。
    /// </summary>
    public static ITiaoGuService Service { get; } =
        new TiaoGuSystem();

    public static bool CanTuneGu(CardModel card, Player player)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(player);

        return card is IGuWormCard &&
            player.PlayerCombatState != null &&
            ReferenceEquals(card.Owner, player) &&
            card.Pile?.Type == GuCardPileSystem.PileType &&
            GuCardPileSystem.PileType
                .GetPile(player)
                .Cards
                .Contains(card) &&
            !GuCardPileSystem.IsTemporaryCapacityBypass(card) &&
            !ShaZhaoTuiYanSystem.IsMaterialSealed(card);
    }

    public static async Task TuneGuAsync(
        CardModel card,
        Player player
    )
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(player);

        SemaphoreSlim gate = OperationGates.GetValue(
            player,
            static _ => new SemaphoreSlim(1, 1)
        );
        await gate.WaitAsync();
        try
        {
            // 同步行动可能排队执行；结算前必须重新校验牌与玩家状态。
            if (!CanTuneGu(card, player))
            {
                return;
            }

            CardModel? previousQueueHead =
                GuCardPileSystem.GetStorageQueuePreview(player, 1)
                    .FirstOrDefault();

            await GuCardPileSystem.MoveCardToPileAsync(
                card,
                GuCardPileSystem.StoragePileType,
                skipVisuals: false
            );

            // MoveCardToPileAsync 已把所选蛊登记到 FIFO 队尾。
            // 统一补位会取当前队首；没有其他候选时，所选蛊会重新回手。
            await GuCardPileSystem.RefillGuHandAsync(player);

            Entry.Logger.Info(
                $"[调蛊] {card.Id} 已进入蛊抽牌队列尾；" +
                $"原队首={(previousQueueHead?.Id.ToString() ?? "无")}。"
            );
        }
        finally
        {
            gate.Release();
        }
    }

    bool ITiaoGuService.CanTuneGu(
        CardModel card,
        Player player
    ) => CanTuneGu(card, player);

    Task ITiaoGuService.TuneGuAsync(
        CardModel card,
        Player player
    ) => TuneGuAsync(card, player);
}
