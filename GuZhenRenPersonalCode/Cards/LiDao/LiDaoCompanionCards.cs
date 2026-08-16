using System.Runtime.CompilerServices;

using GuZhenRen.Cards.HeLian;
using GuZhenRen.Characters;
using GuZhenRen.Multiplayer;
using GuZhenRen.Powers.LiDao;

using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;

namespace GuZhenRen.Cards.LiDao;

public abstract class AbstractLiDaoCompanionCard :
    AbstractGuZhenRenGeneratedCard,
    ILiDaoCompanionCard
{
    public abstract Type SourceGuType { get; }

    protected AbstractLiDaoCompanionCard(
        CardType type,
        TargetType target
    ) : base(1, type, CardRarity.Common, target)
    {
        SetDao(Dao.LiDao);
    }

    /// <summary>
    /// 伴生牌与力道蛊一一对应，卡面转数跟随对应蛊。
    /// 战斗开始生成时直接复制来源蛊转数；此处只负责刷新转数派生值。
    /// canonical 实例（图鉴/卡池）保持默认一转。
    /// </summary>
    protected override void OnGuRankChanged()
    {
        base.OnGuRankChanged();
        RefreshRankValues();
    }

    /// <summary>
    /// 伴生牌卡面头部显示与蛊虫卡一致的中文转数（如“三转”）。
    /// 基类已注入 Rank / Rank1-9Exact；这里补上文言风格的中文数字参数。
    /// </summary>
    protected override void AddExtraArgsToDescription(
        LocString description
    )
    {
        base.AddExtraArgsToDescription(description);
        description.Add("RankCN", ToChineseNumber(GuRank));
    }

    /// <summary>
    /// 按当前转数刷新 DynamicVars 数值（转数基础值 + 升级增量）。
    /// 子类覆写时在构造完成后与蛊升转时调用。
    /// </summary>
    protected virtual void RefreshRankValues()
    {
    }

    /// <summary>本回合是否已打出过攻击牌（不含当前正在结算的这张）。</summary>
    protected bool PlayedAttackEarlierThisTurn()
    {
        CombatManager? combat = CombatManager.Instance;
        return combat != null &&
            combat.History.CardPlaysFinished.Any(
                entry => entry is CardPlayFinishedEntry finished &&
                    finished.HappenedThisTurn(CombatState) &&
                    finished.CardPlay.Player == Owner &&
                    finished.CardPlay.Card.Type == CardType.Attack
            );
    }

}
