// ============================================================================
// 中文维护说明
// 文件职责：实现地灾注入战斗循环的状态牌；对应本地化名称“盐蚀”。
// 主要类型：YanShiStatusCard。
// 实现要点：注册特性把卡牌加入对应卡池，构造器只声明静态费用、类型与目标。
// 维护约定：灾劫选择先持久化再应用；新增钩子时同步更新事件路由和幂等标记。
// ============================================================================
using GuZhenRen.Characters;
using STS2RitsuLib.Interop.AutoRegistration;

namespace GuZhenRen.Tribulations.EarthCalamities.XuanBaiFeiYan;

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class YanShiStatusCard : TribulationStatusCard
{
}
