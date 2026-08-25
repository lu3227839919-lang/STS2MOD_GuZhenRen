// ============================================================================
// 中文维护说明
// 文件职责：实现地灾首领能力及其可同步状态；对应本地化名称“雪怪之灾”。
// 主要类型：XueGuaiPower。
// 实现要点：公开成员构成该模块的稳定协作面；修改签名时应同步检查注册点与调用方。
// 维护约定：灾劫选择先持久化再应用；新增钩子时同步更新事件路由和幂等标记。
// ============================================================================
using GuZhenRen.Tribulations.Core;
using GuZhenRen.Tribulations.Runtime;
using STS2RitsuLib.Interop.AutoRegistration;

namespace GuZhenRen.Tribulations.EarthCalamities.XueGuai;

[RegisterPower]
public sealed class XueGuaiPower : EarthCalamityPower
{
    protected override string IconBaseName => nameof(XueGuaiPower);

    protected override string PrimaryCounterKey =>
        TribulationStateStore.Key(TribulationIds.XueGuai, "nurture_snow");
}
