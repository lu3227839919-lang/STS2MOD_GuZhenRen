// ============================================================================
// 中文维护说明
// 文件职责：实现地灾注入战斗循环的状态牌；对应本地化名称“陨烬”。
// 主要类型：YunJinStatusCard。
// 实现要点：注册特性把卡牌加入对应卡池，构造器只声明静态费用、类型与目标。
// 实现补充：战斗变更通过命令队列并等待完成，不直接绕过游戏同步层修改结果。
// 维护约定：灾劫选择先持久化再应用；新增钩子时同步更新事件路由和幂等标记。
// ============================================================================
using GuZhenRen.Characters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;

namespace GuZhenRen.Tribulations.EarthCalamities.LiuXingHuoYu;

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class YunJinStatusCard : TribulationStatusCard
{
    public override bool HasTurnEndInHandEffect => true;

    protected override async Task OnTurnEndInHand(PlayerChoiceContext choiceContext)
    {
        await CreatureCmd.Damage(
            choiceContext,
            Owner.Creature,
            EarthCalamitySupport.ScaleFlat(Owner, 5),
            ValueProp.Unblockable | ValueProp.Unpowered,
            dealer: null,
            cardSource: this,
            cardPlay: null);
    }
}
