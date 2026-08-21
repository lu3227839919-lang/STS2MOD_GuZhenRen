// ============================================================================
// 中文维护说明
// 文件职责：实现灾劫系统的领域模型、注册表、平衡配置与生成流程。
// 主要类型：TribulationSelection。
// 实现要点：公开成员构成该模块的稳定协作面；修改签名时应同步检查注册点与调用方。
// 维护约定：灾劫选择先持久化再应用；新增钩子时同步更新事件路由和幂等标记。
// ============================================================================
namespace GuZhenRen.Tribulations.Core;

public readonly record struct TribulationSelection(
    string TribulationId,
    TribulationTier Tier,
    TribulationDanger Danger,
    uint LeaderCombatId,
    float MaxHpMultiplier,
    int CurrentRank,
    int CurrentXp,
    int Floor,
    ulong SelectionSeedTag
);
