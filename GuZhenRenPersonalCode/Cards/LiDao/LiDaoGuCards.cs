using GuZhenRen.Cards.HeLian;
using GuZhenRen.Characters;
using GuZhenRen.Combat;
using GuZhenRen.Powers.LiDao;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.ValueProps;

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
            "Training",
            LiDaoBeastTrainingSystem.TrainingRequired
        );
        description.Add(
            "TrainingProgress",
            LiDaoBeastTrainingSystem.GetProgress(this)
        );
        description.Add(
            "TrainingComplete",
            LiDaoBeastTrainingSystem.IsTrainingComplete(this) ? 1 : 0
        );
        bool trainingComplete =
            LiDaoBeastTrainingSystem.IsTrainingComplete(this);
        bool canManifest =
            LiDaoBeastTrainingSystem.WasCompleteAtCombatStart(this);

        description.Add(
            "CanManifestThisCombat",
            canManifest ? 1 : 0
        );
        description.Add(
            "TrainingPending",
            trainingComplete && !canManifest ? 1 : 0
        );
        description.Add(
            "TrainingInProgress",
            trainingComplete ? 0 : 1
        );
    }

}

public abstract class AbstractLiDaoBeastGuCard :
    AbstractLiDaoGuCard,
    ILiDaoBeastGuCard

{
    /// <summary>
    /// 炼力进度主存于卡牌 DynamicVars（随卡牌克隆与存档持久化，
    /// 跨战斗、跨幕继承）；普通实例字段仅作兼容桥接。
    /// GuLiTraining 变量由各具体兽力蛊在 CanonicalVars 中声明。
    /// </summary>
    internal int BeastTrainingProgressBridge { get; set; }

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
        bool canManifest = LiDaoBeastTrainingSystem.RecordEffectiveActivation(
            this,
            cardPlay
        );
        if (canManifest)
        {
            await LiDaoPhantomSystem.ActivateBeastGuAsync<TPhantom>(
                choiceContext,
                this
            );
            return;
        }

        // 未炼力成功：催动提供 1 点力量。
        if (!LiDaoBeastTrainingSystem.IsTrainingComplete(this))
        {
            await PowerCmd.Apply<StrengthPower>(
                choiceContext,
                Owner.Creature,
                1,
                Owner.Creature,
                this
            );
        }
    }

    public override IReadOnlyList<CardModel> GetCarouselCards() =>
        [GuCardReferenceFactory.Create<TPhantom>(this)];
}
