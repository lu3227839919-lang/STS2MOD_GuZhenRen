using GuZhenRen.Cards.XueDao;

using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Powers.XueDao;

[RegisterPower]
public sealed class XueJiPower : ModPowerTemplate
{
    private sealed class TriggerState
    {
        internal CardModel? ActiveCard { get; set; }
    }

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override bool IsVisibleInternal => false;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://GuZhenRenPersonal/images/power/XueJiPower-64x64.png",
        BigIconPath: "res://GuZhenRenPersonal/images/power/XueJiPower-256x256.png"
    );

    protected override object InitInternalData() => new TriggerState();

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (!cardPlay.IsFirstInSeries ||
            cardPlay.IsAutoPlay ||
            !ReferenceEquals(cardPlay.Player.Creature, Owner) ||
            !XueDaoParasiteSystem.HasParasite(cardPlay.Card))
        {
            return Task.CompletedTask;
        }

        TriggerState state = GetInternalData<TriggerState>();
        state.ActiveCard = cardPlay.Card;
        XueDaoParasiteSystem.MarkResolving(cardPlay.Card, true);
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        if (!cardPlay.IsLastInSeries ||
            !ReferenceEquals(cardPlay.Player.Creature, Owner))
        {
            return;
        }

        TriggerState state = GetInternalData<TriggerState>();
        if (!ReferenceEquals(state.ActiveCard, cardPlay.Card))
        {
            return;
        }

        state.ActiveCard = null;
        try
        {
            await XueDaoParasiteSystem.TriggerFromCardPlayAsync(
                choiceContext,
                cardPlay
            );
        }
        finally
        {
            XueDaoParasiteSystem.MarkResolving(cardPlay.Card, false);
            await XueDaoParasiteSystem.ClearIfExhaustedAsync(
                choiceContext,
                cardPlay.Card
            );
        }
    }
}
