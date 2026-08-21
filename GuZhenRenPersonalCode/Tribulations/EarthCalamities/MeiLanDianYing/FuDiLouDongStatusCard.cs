// ============================================================================
// 中文维护说明
// 文件职责：实现地灾注入战斗循环的状态牌；对应本地化名称“伏地漏洞”。
// 主要类型：FuDiLouDongStatusCard。
// 实现要点：注册特性把卡牌加入对应卡池，构造器只声明静态费用、类型与目标。
// 维护约定：灾劫选择先持久化再应用；新增钩子时同步更新事件路由和幂等标记。
// ============================================================================
using GuZhenRen.Characters;
using GuZhenRen.Combat;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;

namespace GuZhenRen.Tribulations.EarthCalamities.MeiLanDianYing;

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class FuDiLouDongStatusCard : TribulationStatusCard
{
    public override async Task AfterCardDrawn(
        PlayerChoiceContext choiceContext,
        CardModel card,
        bool fromHandDraw)
    {
        if (ReferenceEquals(card, this))
        {
            await SecondaryResourceCmd.Spend(
                Owner,
                YuanQiSystem.ResourceId,
                1,
                card: this,
                source: this);
        }
    }
}
