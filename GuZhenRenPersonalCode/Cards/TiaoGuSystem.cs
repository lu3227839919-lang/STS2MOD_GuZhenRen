using System.Runtime.CompilerServices;

using GuZhenRen.Patches;

using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRen.Cards;

/// <summary>
/// 调蛊：右键蛊手牌中的一只常规蛊，把它送入蛊存放 FIFO 队尾，
/// 随后通过统一补位流程把当前队首送入蛊手牌。
/// </summary>
internal static class TiaoGuSystem
{
    private static readonly ConditionalWeakTable<Player, SemaphoreSlim>
        OperationGates = new();

    internal static bool CanTuneGu(CardModel card, Player player)
    {
        if (card is not IGuWormCard ||
            player.PlayerCombatState == null ||
            !ReferenceEquals(card.Owner, player) ||
            card.Pile?.Type != GuCardPileSystem.PileType ||
            GuCardPileSystem.IsOpeningEntryPending(player) ||
            GuCardPlaySyncPatch.IsCardActionExecuting(player) ||
            GuCardPileSystem.IsTemporaryCapacityBypass(card) ||
            ShaZhaoTuiYanSystem.IsMaterialSealed(card))
        {
            return false;
        }

        return GuCardUsageRules.CanUse(card);
    }

    internal static async Task TuneGuAsync(
        CardModel card,
        Player player
    )
    {
        SemaphoreSlim gate = OperationGates.GetValue(
            player,
            static _ => new SemaphoreSlim(1, 1)
        );
        await gate.WaitAsync();
        try
        {
            // 右键动作可能排队执行；结算前必须重新校验牌与玩家状态。
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
}
