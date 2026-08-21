// ============================================================================
// 中文维护说明
// 文件职责：实现蛊真人卡牌、衍生牌及其战斗结算逻辑。
// 主要类型：ZhouDaoCardState。
// 实现要点：卡牌实例状态写入 SavedAttachedState，必须保持复制、存档与联机快照一致。
// 维护约定：修改数值或关键词时同步检查 zhs/eng 本地化；异步战斗效果必须 await。
// ============================================================================
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Utils;

namespace GuZhenRen.Cards.ZhouDao;

internal static class ZhouDaoCardState
{
    private static readonly SavedAttachedState<CardModel, bool>
        XiYingState = new(
            Entry.ModId + ".zhou_dao.xi_ying",
            static () => false
        );

    private static readonly SavedAttachedState<CardModel, int>
        XiYingNianHuaState = new(
            Entry.ModId + ".zhou_dao.xi_ying_nian_hua",
            static () => 0
        );

    internal static void MarkXiYing(CardModel card, int nianHua)
    {
        XiYingState[card] = true;
        XiYingNianHuaState[card] = Math.Max(0, nianHua);
    }

    internal static bool IsXiYing(CardModel card) => XiYingState[card];

    internal static int GetXiYingNianHua(CardModel card) =>
        Math.Max(0, XiYingNianHuaState[card]);
}
