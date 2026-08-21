// ============================================================================
// 中文维护说明
// 文件职责：实现灾劫系统的领域模型、注册表、平衡配置与生成流程。
// 主要类型：TribulationTier、TribulationDanger、TribulationProgressStage。
// 实现要点：公开成员构成该模块的稳定协作面；修改签名时应同步检查注册点与调用方。
// 维护约定：灾劫选择先持久化再应用；新增钩子时同步更新事件路由和幂等标记。
// ============================================================================
namespace GuZhenRen.Tribulations.Core;

public enum TribulationTier
{
    None = 0,
    EarthCalamity = 1,
    HeavenlyTribulation = 2,
    ThousandTribulation = 3,
    GrandTribulation = 4,
    MyriadTribulation = 5,
    HeavenlyDaoBlockade = 6,
}

public enum TribulationDanger
{
    Common,
    Dangerous,
    Aberrant,
}

public enum TribulationProgressStage
{
    Early,
    Mid,
    Late,
    Complete,
}
