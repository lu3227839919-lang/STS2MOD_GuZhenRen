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
public sealed class ZhouMao : AbstractZhouDaoCompanionCard
{
    public override Type SourceGuType => typeof(ZhouMaoXianGu);

    // 与对应蛊虫宙锚仙蛊共用同一张卡图。
    public override CardAssetProfile AssetProfile =>
        CardImageCatalog.Create(typeof(ZhouMaoXianGu));

    protected override void AddExtraArgsToDescription(
        LocString description
    )
    {
        base.AddExtraArgsToDescription(description);

        int gain = GuRank switch
        {
            6 => 1,
            7 or 8 => 2,
            _ => 3,
        };

        description.Add("NianHuaGain", gain);
        description.Add("DrawEverySecond", GuRank >= 8 ? 1 : 0);
    }

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
