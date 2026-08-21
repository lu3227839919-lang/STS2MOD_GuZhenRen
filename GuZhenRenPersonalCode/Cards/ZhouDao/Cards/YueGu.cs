// ============================================================================
// 中文维护说明
// 文件职责：实现蛊真人卡牌、衍生牌及其战斗结算逻辑；对应本地化名称“血月蛊”。
// 主要类型：YueGu。
// 实现要点：注册特性把卡牌加入对应卡池，构造器只声明静态费用、类型与目标。
// 实现补充：OnPlay 使用同步后的 CardPlay 目标和序号执行实际效果。
// 维护约定：修改数值或关键词时同步检查 zhs/eng 本地化；异步战斗效果必须 await。
// ============================================================================
using GuZhenRen.Cards.HeLian;
using GuZhenRen.Characters;
using GuZhenRen.Combat;
using GuZhenRen.Powers.ZhouDao;

using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;

namespace GuZhenRen.Cards.ZhouDao;

[RegisterCard(typeof(GuZhenRenGuCardPool))]
public sealed class YueGu : AbstractZhouDaoGuCard
{
    public override int MinimumAvailableGuRank => 3;

    public override int RecoveryDelayTurns => GuRank switch
    {
        <= 5 => 2,
        <= 8 => 3,
        _ => 4,
    };

    public YueGu() : base(CardRarity.Common)
    {
    }

    protected override void AddExtraArgsToDescription(
        LocString description
    )
    {
        base.AddExtraArgsToDescription(description);

        (int now, int next, int following) = GuRank switch
        {
            3 => (1, 1, 0),
            4 => (1, 2, 0),
            5 => (2, 2, 0),
            6 => (2, 3, 0),
            7 => (2, 2, 2),
            8 => (2, 3, 3),
            _ => (3, 3, 3),
        };

        description.Add("NowGain", now);
        description.Add("NextGain", next);
        description.Add("FollowingGain", following);
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        (int now, int next, int following) = GuRank switch
        {
            3 => (1, 1, 0),
            4 => (1, 2, 0),
            5 => (2, 2, 0),
            6 => (2, 3, 0),
            7 => (2, 2, 2),
            8 => (2, 3, 3),
            _ => (3, 3, 3),
        };

        await ZhouDaoPowerSystem.GainNianHua(
            choiceContext,
            this,
            now
        );
        await YueGuDelayPower.ScheduleAsync(
            choiceContext,
            Owner,
            next,
            following,
            this
        );
    }
}
