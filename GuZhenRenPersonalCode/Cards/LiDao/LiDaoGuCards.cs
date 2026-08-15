using GuZhenRen.Cards.HeLian;
using GuZhenRen.Characters;
using GuZhenRen.Combat;
using GuZhenRen.Powers.LiDao;

using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Utils;

namespace GuZhenRen.Cards.LiDao;

public abstract class AbstractLiDaoGuCard :
    AbstractGuWormCard,
    ILiDaoTrainingGuCard
{
    public abstract int TrainingRequired { get; }

    public abstract Type CompanionCardType { get; }

    /// <summary>
    /// 普通力道蛊的通用恢复回合数。特殊卡牌可在本体中覆写。
    /// </summary>
    public override int RecoveryDelayTurns => GuRank switch
    {
        <= 5 => 2,
        <= 8 => 3,
        _ => 4,
    };

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        base.CanonicalKeywords
            .Append(GuZhenRenKeywords.LianLi)
            .Distinct();

    protected AbstractLiDaoGuCard(
        CardRarity rarity
    ) : base(0, CardType.Skill, rarity, TargetType.Self)
    {
        SetDao(Dao.LiDao);
        this.SecondaryCosts().Set(YuanQiSystem.ResourceId, YuanQiCost);
    }

    protected override void AddExtraArgsToDescription(
        LocString description
    )
    {
        base.AddExtraArgsToDescription(description);
        description.Add("Training", TrainingRequired);
        description.Add(
            "TrainingProgress",
            LiDaoTrainingSystem.GetProgress(this)
        );
    }

}

public abstract class AbstractLiDaoBeastGuCard<TPhantom> :
    AbstractLiDaoGuCard,
    ILiDaoBeastGuCard
    where TPhantom : AbstractLiDaoXuYing
{
    public Type PhantomCardType => typeof(TPhantom);

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        base.CanonicalKeywords
            .Append(GuZhenRenKeywords.NingYing)
            .Distinct();

    protected AbstractLiDaoBeastGuCard(CardRarity rarity) : base(rarity)
    {
    }

    protected override Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    ) => LiDaoPhantomSystem.ActivateBeastGuAsync<TPhantom>(
        choiceContext,
        this
    );

    public override IReadOnlyList<CardModel> GetCarouselCards() =>
        [GuCardReferenceFactory.Create<TPhantom>(this)];
}
