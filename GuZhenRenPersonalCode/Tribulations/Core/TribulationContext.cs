// ============================================================================
// 中文维护说明
// 文件职责：实现灾劫系统的领域模型、注册表、平衡配置与生成流程。
// 主要类型：TribulationSelectionContext、TribulationContext。
// 实现要点：公开成员构成该模块的稳定协作面；修改签名时应同步检查注册点与调用方。
// 维护约定：灾劫选择先持久化再应用；新增钩子时同步更新事件路由和幂等标记。
// ============================================================================
using GuZhenRen.Aperture;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;

namespace GuZhenRen.Tribulations.Core;

public readonly record struct TribulationSelectionContext(
    Player Player,
    ICombatState Combat,
    ApertureRunData RunData,
    int Rank,
    int Xp,
    int RequiredXp,
    int Floor,
    TribulationProgressStage Stage
);

public sealed class TribulationContext
{
    public required Player Player { get; init; }
    public required ICombatState Combat { get; init; }
    public required Creature Leader { get; init; }
    public required TribulationSelection Selection { get; init; }
    public required ApertureRunData RunData { get; init; }

    public int CurrentRank => Selection.CurrentRank;
    public int Floor => Selection.Floor;
}
