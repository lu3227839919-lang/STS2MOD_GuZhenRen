using System.Threading.Tasks;

using GuCard = global::GuZhenRen.Cards.AbstractGuZhenRenCard;



using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace GuZhenRen.Powers;

/// <summary>
/// 变化道根据卡牌流派切换到具体道痕的注册表。
///
/// 第一阶段只登记已经完成移植的道痕。
/// 后续每完成一个道痕，在 switch 中加入一项即可。
/// </summary>
internal static class DaoHenPowerRegistry
{
    /// <summary>
    /// 施加与指定流派对应的道痕。
    /// </summary>
    internal static async Task<bool> TryApplyAsync(
        PlayerChoiceContext choiceContext,
        GuCard.Dao dao,
        Creature owner,
        int amount
    )
    {
        switch (dao)
        {
            case GuCard.Dao.LiDao:
                await PowerCmd.Apply<LiDaoDaoHenPower>(
                    choiceContext,
                    owner,
                    amount,
                    owner,
                    null
                );
                return true;

            case GuCard.Dao.JinDao:
                await PowerCmd.Apply<JinDaoDaoHenPower>(
                    choiceContext,
                    owner,
                    amount,
                    owner,
                    null
                );
                return true;

            case GuCard.Dao.GuDao:
                await PowerCmd.Apply<GuDaoDaoHenPower>(
                    choiceContext,
                    owner,
                    amount,
                    owner,
                    null
                );
                return true;

            case GuCard.Dao.FengDao:
                await PowerCmd.Apply<FengDaoDaoHenPower>(
                    choiceContext,
                    owner,
                    amount,
                    owner,
                    null
                );
                return true;

            case GuCard.Dao.ShaDao:
                await PowerCmd.Apply<ShaDaoDaoHenPower>(
                    choiceContext,
                    owner,
                    amount,
                    owner,
                    null
                );
                return true;

            case GuCard.Dao.XueDao:
                await PowerCmd.Apply<XueDaoDaoHenPower>(
                    choiceContext,
                    owner,
                    amount,
                    owner,
                    null
                );
                return true;

            case GuCard.Dao.LuDao:
                await PowerCmd.Apply<LuDaoDaoHenPower>(
                    choiceContext,
                    owner,
                    amount,
                    owner,
                    null
                );
                return true;

            case GuCard.Dao.ZhiDao:
                await PowerCmd.Apply<ZhiDaoDaoHenPower>(
                    choiceContext,
                    owner,
                    amount,
                    owner,
                    null
                );
                return true;

            case GuCard.Dao.TouDao:
                await PowerCmd.Apply<TouDaoDaoHenPower>(
                    choiceContext,
                    owner,
                    amount,
                    owner,
                    null
                );
                return true;

            case GuCard.Dao.GuangDao:
                await PowerCmd.Apply<GuangDaoDaoHenPower>(
                    choiceContext,
                    owner,
                    amount,
                    owner,
                    null
                );
                return true;

            case GuCard.Dao.YanDao:
                await PowerCmd.Apply<YanDaoDaoHenPower>(
                    choiceContext,
                    owner,
                    amount,
                    owner,
                    null
                );
                return true;

            case GuCard.Dao.MuDao:
                await PowerCmd.Apply<MuDaoDaoHenPower>(
                    choiceContext,
                    owner,
                    amount,
                    owner,
                    null
                );
                return true;

            case GuCard.Dao.JianDao:
                await PowerCmd.Apply<JianDaoDaoHenPower>(
                    choiceContext,
                    owner,
                    amount,
                    owner,
                    null
                );
                return true;

            case GuCard.Dao.ShiDao:
                await PowerCmd.Apply<ShiDaoDaoHenPower>(
                    choiceContext,
                    owner,
                    amount,
                    owner,
                    null
                );
                return true;

            case GuCard.Dao.YunDao:
                await PowerCmd.Apply<YunDaoDaoHenPower>(
                    choiceContext,
                    owner,
                    amount,
                    owner,
                    null
                );
                return true;

            case GuCard.Dao.ZhouDao:
                await PowerCmd.Apply<ZhouDaoDaoHenPower>(
                    choiceContext,
                    owner,
                    amount,
                    owner,
                    null
                );
                return true;

            case GuCard.Dao.TuDao:
                await PowerCmd.Apply<TuDaoDaoHenPower>(
                    choiceContext,
                    owner,
                    amount,
                    owner,
                    null
                );
                return true;

            default:
                return false;
        }
    }
}
