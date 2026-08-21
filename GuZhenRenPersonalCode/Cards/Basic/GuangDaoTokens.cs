// ============================================================================
// 中文维护说明
// 文件职责：定义同一玩法分支共享的衍生牌基类与令牌约定。
// 主要类型：AbstractGuangDaoToken。
// 实现要点：战斗变更通过命令队列并等待完成，不直接绕过游戏同步层修改结果。
// 维护约定：修改数值或关键词时同步检查 zhs/eng 本地化；异步战斗效果必须 await。
// ============================================================================
using GuZhenRen.Characters;
using GuZhenRen.Powers.GuangDao;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.GuangDao;

public abstract class AbstractGuangDaoToken
    : AbstractGuZhenRenGeneratedCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Exhaust];

    protected AbstractGuangDaoToken(int baseCost, CardType type)
        : base(
            baseCost,
            type,
            CardRarity.Token,
            TargetType.Self
        )
    {
        SetDao(Dao.GuangDao);
    }

    protected static async Task ApplyFocus(
        PlayerChoiceContext choiceContext,
        CardModel source,
        int amount
    )
    {
        if (amount <= 0)
        {
            return;
        }

        await PowerCmd.Apply<JuGuangPower>(
            choiceContext,
            source.Owner.Creature,
            amount,
            source.Owner.Creature,
            source
        );
    }
}


