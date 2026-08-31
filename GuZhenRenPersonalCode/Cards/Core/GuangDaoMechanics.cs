namespace GuZhenRen.Cards;

/// <summary>
/// 一次正式出牌序列的折光结果。Triggered 只表示真实折光；
/// EffectResolutionCount 表示卡牌自身折光效果应结算的次数。
/// </summary>
public readonly record struct RefractionResult(
    bool Triggered,
    int EffectResolutionCount
)
{
    public static RefractionResult None => new(false, 0);
}

/// <summary>声明该牌具有可被聚光复制的折光效果。</summary>
public interface IRefractionEffectCard
{
}

/// <summary>声明该牌需要展示新版折光说明。</summary>
public interface IRefractionRelevantCard
{
}

/// <summary>声明该牌会获得或产生聚光。</summary>
public interface IJuGuangCard
{
}

/// <summary>声明该牌使用现有调蛊服务。</summary>
public interface ITiaoGuCard
{
}

/// <summary>声明该牌使用月华累计。</summary>
public interface IMoonlightCard
{
}
