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
