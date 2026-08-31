using MegaCrit.Sts2.Core.Entities.Cards;

namespace GuZhenRen.Cards.XueDao;

/// <summary>血道战斗衍生牌的公共基类。新版只保留遗骸。</summary>
public abstract class AbstractXueDaoToken : AbstractGuZhenRenGeneratedCard
{
    protected AbstractXueDaoToken(
        int baseCost,
        CardType type,
        CardRarity rarity,
        TargetType target
    ) : base(baseCost, type, rarity, target)
    {
        SetDao(Dao.XueDao);
    }
}
