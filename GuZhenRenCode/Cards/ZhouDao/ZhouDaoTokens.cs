using GuZhenRen.Characters;
using GuZhenRen.Powers.ZhouDao;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.ZhouDao;

public abstract class AbstractZhouDaoToken :
    AbstractGuZhenRenCard,
    ICardRewardExcluded
{
    public override CardPoolModel Pool => ModelDb.CardPool<GuZhenRenCardPool>();
    public override bool CanBeGeneratedInCombat => false;
    public override int MaxUpgradeLevel => 0;
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Exhaust, GuZhenRenKeywords.NianHua, GuZhenRenKeywords.SuiMan];

    protected AbstractZhouDaoToken()
        : base(0, CardType.Skill, CardRarity.Token, TargetType.Self)
    {
        SetDao(Dao.ZhouDao);
    }
}

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class NianLiu : AbstractZhouDaoToken
{
    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        await ZhouDaoPowerSystem.GainNianHua(choiceContext, this, 2);
    }
}

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class NianLiuPlus : AbstractZhouDaoToken
{
    // 与年流共用同一张卡图。
    public override CardAssetProfile AssetProfile =>
        CardImageCatalog.Create(typeof(NianLiu));

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        await ZhouDaoPowerSystem.GainNianHua(choiceContext, this, 3);
        await CardPileCmd.Draw(choiceContext, 1, Owner);
    }
}
