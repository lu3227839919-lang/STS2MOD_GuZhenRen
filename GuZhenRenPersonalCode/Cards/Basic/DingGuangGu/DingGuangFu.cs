// ============================================================================
// 中文维护说明
// 文件职责：实现蛊真人卡牌、衍生牌及其战斗结算逻辑；对应本地化名称“定光符”。
// 主要类型：DingGuangFu。
// 实现要点：注册特性把卡牌加入对应卡池，构造器只声明静态费用、类型与目标。
// 实现补充：OnPlay 使用同步后的 CardPlay 目标和序号执行实际效果。
// 实现补充：OnUpgrade 只维护升级差值，基础值仍由 DynamicVars 统一提供。
// 维护约定：修改数值或关键词时同步检查 zhs/eng 本地化；异步战斗效果必须 await。
// ============================================================================
using GuZhenRen.Characters;
using GuZhenRen.Multiplayer;
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

namespace GuZhenRen.Cards.GuangDao;

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class DingGuangFu : AbstractLightExpansionToken
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("BonusDamage", 5m)];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        base.CanonicalKeywords
            .Append(GuZhenRenKeywords.YingGuang)
            .Distinct();

    public override CardAssetProfile AssetProfile =>
        global::GuZhenRen.Cards.CardImageCatalog.Create(GetType());

    public DingGuangFu() : base(0, CardType.Skill, TargetType.Self)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        await PowerCmd.Apply<DingGuangChargePower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["BonusDamage"].IntValue,
            Owner.Creature,
            this
        );

        if (GuRank >= 7 && cardPlay.PlayIndex == 0)
        {
            GuangBiao marker = GuGeneratedCardFactory.Create<GuangBiao>(
                Owner,
                GuRank
            );
            await GuGeneratedCardFactory.AddToHandOrDiscard(
                marker,
                Owner
            );
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["BonusDamage"].UpgradeValueBy(3m);
    }
}
