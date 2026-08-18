using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRen.Cards;

/// <summary>
/// 调蛊核心服务接口。
///
/// 调用方负责选择一张目标蛊；服务统一校验目标是否位于该玩家的蛊手牌，
/// 随后把目标放入蛊存放 FIFO 队尾，并从当前队首补位。
/// 调蛊没有默认鼠标或键盘绑定，后续蛊虫效果应在同步行动中调用本接口。
/// </summary>
public interface ITiaoGuService
{
    bool CanTuneGu(CardModel card, Player player);

    Task TuneGuAsync(CardModel card, Player player);
}
