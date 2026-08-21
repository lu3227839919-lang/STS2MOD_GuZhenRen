// ============================================================================
// 中文维护说明
// 文件职责：实现蛊真人卡牌、衍生牌及其战斗结算逻辑。
// 主要类型：GuGeneratedCardFactory。
// 实现要点：战斗衍生牌必须由当前 CombatState 创建，确保网络卡号和牌堆归属有效。
// 实现补充：战斗变更通过命令队列并等待完成，不直接绕过游戏同步层修改结果。
// 维护约定：修改数值或关键词时同步检查 zhs/eng 本地化；异步战斗效果必须 await。
// ============================================================================
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRen.Cards;

/// <summary>
/// 战斗内蛊真人衍生牌的统一工厂。
/// <para>
/// 由当前 <c>CombatState</c> 创建卡牌，保证网络卡号、所有者和战斗牌数据库
/// 完整；随后复制来源品阶，并按需应用原生升级。
/// </para>
/// </summary>
internal static class GuGeneratedCardFactory
{
    /// <summary>创建一张带来源品阶的战斗衍生牌；战斗外调用会抛错。</summary>
    internal static T Create<T>(
        Player owner,
        int guRank,
        bool upgraded = false
    ) where T : AbstractGuZhenRenCard
    {
        if (owner.Creature.CombatState is not { } combatState)
        {
            throw new InvalidOperationException(
                "Cannot create a generated Gu card outside combat."
            );
        }

        T card = (T)combatState.CreateCard(
            ModelDb.Card<T>(),
            owner
        );
        card.InitializeGuRankFromSource(guRank);

        if (upgraded)
        {
            CardCmd.Upgrade(card);
        }

        return card;
    }

    /// <summary>优先加入手牌；手牌无法接收时回退到弃牌堆，避免静默丢牌。</summary>
    internal static async Task AddToHandOrDiscard(
        CardModel card,
        Player owner
    )
    {
        bool added = await GuCardPileSystem.AddGeneratedCardToHand(
            card,
            owner
        );

        if (!added)
        {
            await GuCardPileSystem.AddGeneratedCardToDiscard(
                card,
                owner
            );
        }
    }
}
