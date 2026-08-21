// ============================================================================
// 中文维护说明
// 文件职责：定义灾劫系统的扩展契约、事件载荷与能力组合接口。
// 主要类型：ITribulationDefinition。
// 实现要点：灾劫能力按所实现的细粒度接口由事件路由选择性分派。
// 维护约定：灾劫选择先持久化再应用；新增钩子时同步更新事件路由和幂等标记。
// ============================================================================
using GuZhenRen.Tribulations.Core;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRen.Tribulations.Contracts;

public interface ITribulationDefinition
{
    string Id { get; }
    TribulationTier Tier { get; }
    TribulationDanger Danger { get; }
    int BaseWeight { get; }
    bool CanAppear(in TribulationSelectionContext context);
    float GetEnemyCompatibilityMultiplier(in TribulationSelectionContext context);
}
