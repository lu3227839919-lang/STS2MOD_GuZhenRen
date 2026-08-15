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

/// <summary>
/// 遗骸：寄生牌完成合法击杀或流血致死后获得。主动使用恢复2点血元后
/// 立即消耗消失；未被使用的遗骸在战斗结束后保留到永久牌堆（最多4张），
/// 也可被血颅蛊、刀翅血蝠蛊及相关杀招主动消耗。
/// </summary>
[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class YiHai : AbstractXueDaoToken
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new PowerVar<XueYuanPower>(2m)];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Retain];

    public YiHai()
        : base(
            0,
            CardType.Status,
            CardRarity.Status,
            TargetType.Self
        )
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        await XueDaoPowerSystem.GainXueYuanFromCardEffect(
            choiceContext,
            this,
            DynamicVars[typeof(XueYuanPower).Name].IntValue
        );

        // 遗骸使用后立即消失（进入消耗堆），不可在永久牌堆中复用；
        // 未使用的遗骸作为非消耗牌在战斗结束后保留至永久牌堆。
        // 遗骸若是永久牌组卡的战斗克隆体（DeckVersion 指向 Deck 原件），
        // 消耗时同步从永久牌组删除原件，确保“打出即永久消耗”，
        // 与原版 SwipePower/DeprecatedCard 的取骸/移除先例一致。
        await XueDaoCardSystem.ConsumeRemainCard(
            choiceContext,
            this
        );
    }

    protected override void OnUpgrade()
    {
    }
}
