// ============================================================================
// 中文维护说明
// 文件职责：实现蛊真人卡牌、衍生牌及其战斗结算逻辑；对应本地化名称“血蝠王”。
// 主要类型：XueFuWang。
// 实现要点：注册特性把卡牌加入对应卡池，构造器只声明静态费用、类型与目标。
// 维护约定：修改数值或关键词时同步检查 zhs/eng 本地化；异步战斗效果必须 await。
// ============================================================================
using GuZhenRen.Characters;
using GuZhenRen.Multiplayer;
using GuZhenRen.Powers.XueDao;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.XueDao;

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class XueFuWang : AbstractBloodBatToken
{
    private const string ConsumedRemainsVar = "ConsumedRemains";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        base.CanonicalVars.Concat(
            [new DynamicVar(ConsumedRemainsVar, 0m)]
        );

    protected override int ExtraBaseHits =>
        DynamicVars[ConsumedRemainsVar].IntValue * 2;

    protected override bool TransfersOnKill =>
        DynamicVars[ConsumedRemainsVar].IntValue >= 2;

    public XueFuWang() : base(2)
    {
    }

    internal void ConfigureConsumedRemains(int amount)
    {
        int consumed = Math.Clamp(amount, 0, 2);
        DynamicVars[ConsumedRemainsVar].BaseValue = consumed;

        if (consumed >= 2)
        {
            AddKeyword(GuZhenRenKeywords.ZhuiJi);
        }
    }
}
