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
public sealed class NianNianSuiSui : AbstractZhouDaoCompanionCard
{
    public override Type SourceGuType => typeof(NianGu);

    // 与对应蛊虫年蛊共用同一张卡图。
    public override CardAssetProfile AssetProfile =>
        CardImageCatalog.Create(typeof(NianGu));

    protected override void AddExtraArgsToDescription(
        LocString description
    )
    {
        base.AddExtraArgsToDescription(description);

        int turnInterval = GuRank switch
        {
            1 => 3,
            2 or 3 => 2,
            _ => 1,
        };
        int turnGain = GuRank >= 8 ? 2 : 1;

        description.Add("TurnInterval", turnInterval);
        description.Add("TurnGain", turnGain);
        description.Add("EveryTurn", turnInterval == 1 ? 1 : 0);
        description.Add("HasSuiManBonus", GuRank is 5 or 6 or 7 or 9 ? 1 : 0);
        description.Add("FirstSuiManOnly", GuRank == 5 ? 1 : 0);
        description.Add("EverySecondSuiMan", GuRank == 6 ? 1 : 0);
        description.Add("EverySuiMan", GuRank is 7 or 9 ? 1 : 0);
    }

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
