using GuZhenRen.Characters;

using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Interop.AutoRegistration;

namespace GuZhenRen.Cards.XueDao;

/// <summary>遗骸是0费保留状态牌，但不能主动打出，只能由炼骸移除。</summary>
[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class YiHai : AbstractXueDaoToken
{
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Retain];

    protected sealed override bool IsPlayable => false;

    public YiHai()
        : base(0, CardType.Status, CardRarity.Status, TargetType.Self)
    {
    }

    protected override Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    ) => Task.CompletedTask;

    protected override void OnUpgrade()
    {
    }
}
