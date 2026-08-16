using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace GuZhenRen.Cards.LiDao;

/// <summary>五种可独立显化、也可被百兽力蛊收纳的兽力。</summary>
public enum LiDaoBeastKind
{
    BaiZhi = 0,
    FeiXiong = 1,
    E = 2,
    QingNiu = 3,
    ShiGui = 4,
}

/// <summary>需要在战斗开始时进入蛊封存堆、通过伴生牌练力解封的蛊。</summary>
public interface ILiDaoTrainingGuCard : IGuWormCard
{
    int TrainingRequired { get; }

    Type CompanionCardType { get; }
}

/// <summary>练满后可把多余练力转化为群力层数的力道蛊。</summary>
public interface ILiDaoExtraTrainingGuCard : ILiDaoTrainingGuCard
{
}

/// <summary>催动后生成常驻兽力虚影的力道蛊。</summary>
public interface ILiDaoBeastGuCard : ILiDaoTrainingGuCard
{
    Type PhantomCardType { get; }
}

/// <summary>所有力道伴生普通牌的公共契约。</summary>
public interface ILiDaoCompanionCard
{
    Type TrainedGuType { get; }
}

/// <summary>力道集中结算器读取虚影类型、排序与容量信息的契约。</summary>
public interface ILiDaoPhantomCard
{
    LiDaoBeastKind? BeastKind { get; }

    bool IsFullForcePhantom { get; }

    int PhantomSlotCost { get; }
}

internal static class LiDaoCardTypeMap
{
    internal static Type GetCompanionType(CardModel guCard) => guCard switch
    {
        ILiDaoTrainingGuCard trainingGu => trainingGu.CompanionCardType,
        _ => throw new ArgumentException(
            "卡牌不是需要练力的力道蛊。",
            nameof(guCard)
        ),
    };
}
