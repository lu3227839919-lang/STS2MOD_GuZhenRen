// ============================================================================
// 中文维护说明
// 文件职责：连接空窍成长状态与灾劫选择、保存及恢复流程。
// 主要类型：ApertureTribulationExtensions。
// 实现要点：公开成员构成该模块的稳定协作面；修改签名时应同步检查注册点与调用方。
// 维护约定：保持公开签名、存档键和多人确定性；异步命令必须等待结算完成。
// ============================================================================
using GuZhenRen.Tribulations.Core;

namespace GuZhenRen.Aperture;

public static class ApertureTribulationExtensions
{
    public static TribulationSelection ToTribulationSelection(this ApertureRunData data) => new(
        data.ActiveTribulationId,
        (TribulationTier)data.ActiveTribulationTier,
        (TribulationDanger)data.ActiveTribulationDanger,
        data.ActiveLeaderCombatId,
        data.ActiveTribulationMaxHpMultiplier,
        data.Rank,
        data.Xp,
        data.ActiveTribulationFloor,
        data.ActiveTribulationSelectionSeedTag);
}
