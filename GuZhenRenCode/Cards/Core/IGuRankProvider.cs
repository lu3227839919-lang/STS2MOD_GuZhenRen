namespace GuZhenRen.Cards;

/// <summary>
/// 提供蛊虫转数的卡牌。
///
/// GuRank 与游戏原生升级完全独立：
///
/// - OnUpgrade / IsUpgraded 表示卡牌升级；
/// - GuRank / TryIncreaseGuRank 表示蛊虫升转。
///
/// 普通升级不会提高 GuRank；
/// 升转也不会调用 OnUpgrade 或改变 IsUpgraded。
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
public interface IGuWormCard : IGuRankProvider
{
}
