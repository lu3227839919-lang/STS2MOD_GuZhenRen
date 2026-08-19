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
public sealed class GuangYinRenRan : AbstractZhouDaoCompanionCard
{
    public override Type SourceGuType => typeof(GuangYinRenRanGu);

    // 与对应蛊虫光阴荏苒蛊共用同一张卡图。
    public override CardAssetProfile AssetProfile =>
        CardImageCatalog.Create(typeof(GuangYinRenRanGu));

    protected override void AddExtraArgsToDescription(
        LocString description
    )
    {
        base.AddExtraArgsToDescription(description);

        int limit = GuRank switch
        {
            <= 2 => 1,
            <= 5 => 2,
            _ => 3,
        };
        int acceleratedBonus = GuRank switch
        {
            5 or 7 => 1,
            >= 8 => 2,
            _ => 0,
        };

        description.Add("RecoveryLimit", limit);
        description.Add("AcceleratedBonus", acceleratedBonus);
        description.Add(
            "HasAcceleratedBonus",
            acceleratedBonus > 0 ? 1 : 0
        );
        description.Add("DrawOnAccelerated", GuRank >= 9 ? 1 : 0);
    }

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
