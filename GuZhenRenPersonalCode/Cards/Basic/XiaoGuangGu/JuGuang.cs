using GuZhenRen.Characters;
using GuZhenRen.Powers.GuangDao;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.GuangDao;

[RegisterCard(typeof(GuZhenRenCardPool))]
public sealed class JuGuang : AbstractGuangDaoToken
{
    public override CardAssetProfile AssetProfile =>
        global::GuZhenRen.Cards.CardImageCatalog.Create(GetType());

    public JuGuang() : base(0, CardType.Skill)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        await ApplyFocus(
            choiceContext,
            this,
            IsUpgraded ? 8 : 6
        );

        if (GuRank >= 8 && cardPlay.PlayIndex == 0)
        {
            YuHui afterglow = GuGeneratedCardFactory.Create<YuHui>(
                Owner,
                GuRank
            );
            await GuGeneratedCardFactory.AddToHandOrDiscard(
                afterglow,
                Owner
            );
        }
    }

    protected override void OnUpgrade()
    {
    }
}
