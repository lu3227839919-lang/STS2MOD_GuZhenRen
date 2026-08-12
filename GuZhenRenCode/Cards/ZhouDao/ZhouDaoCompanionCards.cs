using GuZhenRen.Characters;
using GuZhenRen.Powers.ZhouDao;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.ZhouDao;

public abstract class AbstractZhouDaoCompanionCard :
    AbstractGuZhenRenCard,
    IZhouDaoCompanionCard,
    ICardRewardExcluded
{
    public abstract Type SourceGuType { get; }

    public override bool CanBeGeneratedInCombat => false;
    public override int MaxUpgradeLevel => 0;
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        base.CanonicalKeywords
            .Append(GuZhenRenKeywords.NianHua)
            .Append(GuZhenRenKeywords.SuiMan)
            .Distinct();

    protected AbstractZhouDaoCompanionCard()
        : base(1, CardType.Power, CardRarity.Common, TargetType.Self)
    {
        SetDao(Dao.ZhouDao);
    }

    protected override void OnGuRankChanged()
    {
        base.OnGuRankChanged();
        ZhouDaoCompanionSystem.SyncFromCompanion(this);
    }

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        description.Add("RankCN", ToChineseNumber(GuRank));
    }
}

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class GuangYinRenRan : AbstractZhouDaoCompanionCard
{
    public override Type SourceGuType => typeof(GuangYinRenRanGu);

    // 与对应蛊虫光阴荏苒蛊共用同一张卡图。
    public override CardAssetProfile AssetProfile =>
        CardImageCatalog.Create(typeof(GuangYinRenRanGu));

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        GuangYinRenRanPower? power = await PowerCmd.Apply<GuangYinRenRanPower>(
            choiceContext,
            Owner.Creature,
            1,
            Owner.Creature,
            this
        );
        power?.SetRank(GuRank);
    }
}

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class NianNianSuiSui : AbstractZhouDaoCompanionCard
{
    public override Type SourceGuType => typeof(NianGu);

    // 与对应蛊虫年蛊共用同一张卡图。
    public override CardAssetProfile AssetProfile =>
        CardImageCatalog.Create(typeof(NianGu));

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        NianNianSuiSuiPower? power = await PowerCmd.Apply<NianNianSuiSuiPower>(
            choiceContext,
            Owner.Creature,
            1,
            Owner.Creature,
            this
        );
        power?.SetRank(GuRank);
    }
}

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class ZhouMao : AbstractZhouDaoCompanionCard
{
    public override Type SourceGuType => typeof(ZhouMaoXianGu);

    // 与对应蛊虫宙锚仙蛊共用同一张卡图。
    public override CardAssetProfile AssetProfile =>
        CardImageCatalog.Create(typeof(ZhouMaoXianGu));

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        ZhouMaoPower? power = await PowerCmd.Apply<ZhouMaoPower>(
            choiceContext,
            Owner.Creature,
            1,
            Owner.Creature,
            this
        );
        power?.SetRank(GuRank);
    }
}

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class SiShuiLiuNian : AbstractZhouDaoCompanionCard
{
    public override Type SourceGuType => typeof(SiShuiLiuNianXianGu);

    // 与对应蛊虫似水流年仙蛊共用同一张卡图。
    public override CardAssetProfile AssetProfile =>
        CardImageCatalog.Create(typeof(SiShuiLiuNianXianGu));

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        SiShuiLiuNianPower? power = await PowerCmd.Apply<SiShuiLiuNianPower>(
            choiceContext,
            Owner.Creature,
            1,
            Owner.Creature,
            this
        );
        power?.SetRank(GuRank);
    }
}
