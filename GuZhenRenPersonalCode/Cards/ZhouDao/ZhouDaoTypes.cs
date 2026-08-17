using MegaCrit.Sts2.Core.Models;

namespace GuZhenRen.Cards.ZhouDao;

/// <summary>进入战斗时会在抽牌堆生成伴生能力牌的宙道蛊。</summary>
public interface IZhouDaoCompanionGuCard : ICompanionSourceGuCard
{
}

/// <summary>宙道伴生普通牌（转数跟随对应蛊）。</summary>
public interface IZhouDaoCompanionCard : ICompanionCard
{
}

/// <summary>宙道生成的“昔影”标记接口仅用于类型排除。</summary>
public interface IXiYingCard
{
}
