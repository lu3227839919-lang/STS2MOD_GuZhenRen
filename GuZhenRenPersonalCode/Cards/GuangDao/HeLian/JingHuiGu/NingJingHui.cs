// ============================================================================
// 中文维护说明
// 文件职责：实现蛊真人卡牌、衍生牌及其战斗结算逻辑；对应本地化名称“凝镜辉”。
// 主要类型：NingJingHui。
// 实现要点：注册特性把卡牌加入对应卡池，构造器只声明静态费用、类型与目标。
// 实现补充：OnPlay 使用同步后的 CardPlay 目标和序号执行实际效果。
// 实现补充：OnUpgrade 只维护升级差值，基础值仍由 DynamicVars 统一提供。
// 维护约定：修改数值或关键词时同步检查 zhs/eng 本地化；异步战斗效果必须 await。
// ============================================================================
using GuZhenRen.Characters;
using GuZhenRen.Powers.GuangDao;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.HeLian;

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class NingJingHui : AbstractJingHuiToken
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(14m, ValueProp.Move),
        new DynamicVar("LightBonus", 5m),
        new DynamicVar("ZhaoPoBonus", 6m),
    ];

    public override bool GainsBlock => true;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        base.CanonicalKeywords
            .Append(global::GuZhenRen.Cards.GuZhenRenKeywords.YingGuang)
            .Distinct();

    public NingJingHui() : base(1)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        await CreatureCmd.GainBlock(
            Owner.Creature,
            DynamicVars.Block,
            cardPlay
        );
        await PowerCmd.Apply<JuGuangPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["LightBonus"].IntValue,
            Owner.Creature,
            this
        );
        await PowerCmd.Apply<DingGuangChargePower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["ZhaoPoBonus"].IntValue,
            Owner.Creature,
            this
        );

        if (GuRank >= 7 && cardPlay.PlayIndex == 0)
        {
            FanHui returned = GuGeneratedCardFactory.Create<FanHui>(
                Owner,
                GuRank
            );
            await GuGeneratedCardFactory.AddToHandOrDiscard(
                returned,
                Owner
            );
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3m);
        DynamicVars["LightBonus"].UpgradeValueBy(2m);
        DynamicVars["ZhaoPoBonus"].UpgradeValueBy(2m);
    }
}
