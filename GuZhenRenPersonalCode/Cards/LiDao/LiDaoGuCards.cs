using GuZhenRen.Cards.HeLian;
using GuZhenRen.Characters;
using GuZhenRen.Combat;
using GuZhenRen.Powers.LiDao;

using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;

using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Utils;

namespace GuZhenRen.Cards.LiDao;

public abstract class AbstractLiDaoGuCard :
    AbstractGuWormCard
{
    public abstract Type CompanionCardType { get; }

    public override int MinimumAvailableGuRank => 2;

    public override int MaxGuRank => 5;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        base.CanonicalKeywords
            .Append(GuZhenRenKeywords.LianLi)
            .Distinct();

    protected AbstractLiDaoGuCard(
        CardRarity rarity
    ) : base(0, CardType.Power, rarity, TargetType.Self)
    {
        SetDao(Dao.LiDao);
        this.SecondaryCosts().Set(YuanQiSystem.ResourceId, YuanQiCost);
    }

    protected override void AddExtraArgsToDescription(
        LocString description
    )
    {
        base.AddExtraArgsToDescription(description);
        description.Add(
            "TrainingRequired",
            LiDaoBeastTrainingSystem.TrainingRequired
        );
        description.Add(
            "TrainingProgress",
            LiDaoBeastTrainingSystem.GetProgress(this)
        );
        description.Add(
            "TrainingUnlocked",
            LiDaoBeastTrainingSystem.IsUnlocked(this) ? 1 : 0
        );
        description.Add(
            "TrainingSealed",
            LiDaoBeastTrainingSystem.IsTrainingSealed(this) ? 1 : 0
        );
    }

}

public abstract class AbstractLiDaoBeastGuCard :
    AbstractLiDaoGuCard,
    ILiDaoBeastGuCard

{
    protected AbstractLiDaoBeastGuCard(CardRarity rarity) : base(rarity)
    {
    }

    public abstract Type PhantomCardType { get; }
}

public abstract class AbstractLiDaoBeastGuCard<TPhantom> :
    AbstractLiDaoBeastGuCard
    where TPhantom : AbstractLiDaoXuYing
{
    public override Type PhantomCardType => typeof(TPhantom);

    protected AbstractLiDaoBeastGuCard(CardRarity rarity) : base(rarity)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        if (!cardPlay.IsFirstInSeries)
        {
            return;
        }

        if (!LiDaoBeastTrainingSystem.IsUnlocked(this) ||
            GuSealSystem.IsSealed(this))
        {
            Entry.Logger.Warn(
                $"[兽力蛊] 拒绝催动尚未解封的 {Id}：" +
                $"progress={LiDaoBeastTrainingSystem.GetProgress(this)}/" +
                $"{LiDaoBeastTrainingSystem.TrainingRequired}, " +
                $"seal={GuSealSystem.GetSealReason(this)}。"
            );
            return;
        }

        await LiDaoPhantomSystem.ActivateBeastGuAsync<TPhantom>(
            choiceContext,
            this
        );
    }

    public override IReadOnlyList<CardModel> GetCarouselCards() =>
        [GuCardReferenceFactory.Create<TPhantom>(this)];
}
