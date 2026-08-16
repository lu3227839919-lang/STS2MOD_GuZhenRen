using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRen.Cards.LiDao;

/// <summary>五种可独立显化的兽力。</summary>
public enum LiDaoBeastKind
{
    BaiZhi = 0,
    FeiXiong = 1,
    E = 2,
    QingNiu = 3,
    ShiGui = 4,
}

/// <summary>拥有永久 0/3 炼力进度，炼成后催动可生成虚影的兽力蛊。</summary>
public interface ILiDaoBeastGuCard : IGuWormCard
{
    Type CompanionCardType { get; }

    Type PhantomCardType { get; }
}

/// <summary>所有力道伴生普通牌的公共契约。</summary>
public interface ILiDaoCompanionCard
{
    Type SourceGuType { get; }
}

/// <summary>力道集中结算器读取虚影类型、排序与容量信息的契约。</summary>
public interface ILiDaoPhantomCard
{
    LiDaoBeastKind? BeastKind { get; }

    int PhantomSlotCost { get; }
}
