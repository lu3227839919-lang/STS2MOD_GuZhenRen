namespace GuZhenRen.Cards;

/// <summary>
/// 具有基础触发概率的卡牌接口。
/// </summary>
public interface IProbabilityCard
{
    /// <summary>
    /// 当前基础触发概率，范围为 0～1。
    /// </summary>
    float BaseChance { get; }

    /// <summary>
    /// 增加或减少基础触发概率。
    /// </summary>
    void IncreaseBaseChance(float amount);
}
