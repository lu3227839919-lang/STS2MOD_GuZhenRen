namespace GuZhenRen.Cards;

/// <summary>
/// 提供蛊虫转数的卡牌。
///
/// GuRank 表示卡牌体系中的转数。
///
/// 真正蛊虫牌不参与游戏原生卡牌升级；包括五转到六转在内，
/// 所有转数变化均由升炼、奖励赋阶、合炼或存档恢复处理。
/// 六转及以上真正蛊虫仅在状态语义上视为已升级。
/// 杀招、虚影等其他实现者可以保留自己的升级规则。
/// </summary>
public interface IGuRankProvider
{
    int GuRank { get; }
}


/// <summary>
/// 标记一张可作为蛊虫系统材料的真正蛊虫卡。
///
/// 该接口继承 IGuRankProvider，但用途更严格：
/// 只有实现本接口的卡牌才允许作为合练材料。
/// 仙元、杀招推演等即使具有其他系统数值，也不应实现此接口。
/// </summary>
public interface IGuWormCard
    : IGuRankProvider
{
    /// <summary>
    /// 催动该蛊虫需要支付的元气。普通蛊默认 1，合练蛊通常为 2。
    /// 该值用于蛊手牌阶段的即时可用性检查，避免选中目标后才因元气不足抛错。
    /// </summary>
    int YuanQiCost => 1;

    /// <summary>
    /// 同一张蛊虫在进入蛊手牌后总共可催动的次数。
    /// 剩余次数跨回合保留；耗尽后进入蛊冷却堆，冷却完成时重新充满。
    /// </summary>
    int MaxUses => 1;

    /// <summary>
    /// 催动次数耗尽后需要等待的回合数。
    /// 例如耗尽回合为 2、RecoveryDelayTurns 为 2 时，
    /// 会在第 4 回合开始恢复。具体蛊虫可以按转数重写。
    /// </summary>
    int RecoveryDelayTurns => 2;
}
