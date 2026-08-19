using GuZhenRen.Cards.HeLian;
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

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class SiShuiLiuNian : AbstractZhouDaoCompanionCard
{
    public override Type SourceGuType => typeof(SiShuiLiuNianXianGu);

    // 与对应蛊虫似水流年仙蛊共用同一张卡图。
    public override CardAssetProfile AssetProfile =>
        CardImageCatalog.Create(typeof(SiShuiLiuNianXianGu));

    protected override void AddExtraArgsToDescription(
        LocString description
    )
    {
        base.AddExtraArgsToDescription(description);
        description.Add("PlusToken", GuRank >= 9 ? 1 : 0);
    }

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
