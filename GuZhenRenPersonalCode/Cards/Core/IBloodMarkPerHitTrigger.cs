// ============================================================================
// 中文维护说明
// 文件职责：定义卡牌系统跨模块调用的扩展接口。
// 主要类型：IBloodMarkPerHitTrigger。
// 实现要点：公开成员构成该模块的稳定协作面；修改签名时应同步检查注册点与调用方。
// 维护约定：修改数值或关键词时同步检查 zhs/eng 本地化；异步战斗效果必须 await。
// ============================================================================
namespace GuZhenRen.Cards;

/// <summary>
/// Marker for Blood Path Gu attack cards whose individual hits may each
/// consume one Blood Mark. Cards without this marker can trigger Blood Mark
/// at most once per CardPlay, regardless of hit count.
/// </summary>
public interface IBloodMarkPerHitTrigger
{
}
