// ============================================================================
// 中文维护说明
// 文件职责：实现地灾注入战斗循环的状态牌；对应本地化名称“雪封劫痕”。
// 主要类型：XueFengJieHenGuCorruptionCard。
// 实现要点：注册特性把卡牌加入对应卡池，构造器只声明静态费用、类型与目标。
// 维护约定：灾劫选择先持久化再应用；新增钩子时同步更新事件路由和幂等标记。
// ============================================================================
using GuZhenRen.Aperture;
using GuZhenRen.Characters;
using GuZhenRen.Tribulations.Contracts;
using GuZhenRen.Tribulations.Core;
using GuZhenRen.Tribulations.Runtime;
using STS2RitsuLib.Interop.AutoRegistration;

namespace GuZhenRen.Tribulations.EarthCalamities.XueYue;

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class XueFengJieHenGuCorruptionCard :
    TribulationStatusCard,
    IGuSystemCorruptionCard,
    IGuActiveSlotOccupant
{
    protected override bool ExhaustAtTurnEnd => false;

    public int OccupiedActiveSlots
    {
        get
        {
            if (!IsMutable || Owner == null)
                return 1;
            int cold = ApertureSystem.GetState(Owner)
                .TribulationState
                .GetCounter(TribulationStateStore.Key(
                    TribulationIds.XueYue,
                    "accumulated_cold"));
            return cold >= 4 ? 2 : 1;
        }
    }
}
