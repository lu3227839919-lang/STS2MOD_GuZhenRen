using MegaCrit.Sts2.Core.Models;

namespace GuZhenRen.Cards.ZhouDao;

/// <summary>带有永久普通牌伴生能力的宙道蛊。</summary>
public interface IZhouDaoCompanionGuCard : IGuWormCard
{
    Type CompanionCardType { get; }
}

/// <summary>宙道伴生普通牌。</summary>
public interface IZhouDaoCompanionCard
{
    Type SourceGuType { get; }
}

/// <summary>宙道生成的“昔影”标记接口仅用于类型排除。</summary>
public interface IXiYingCard
{
}
