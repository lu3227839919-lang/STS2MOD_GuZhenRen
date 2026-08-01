using GuZhenRen.Cards.Basic;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.ImmortalEssence;

/// <summary>
/// 黄杏仙元：获得 5 点能量，然后抽牌直到手牌达到当前上限。
/// </summary>
public sealed class HuangXingXianYuan : AbstractXianYuanCard
{
    protected override int EnergyGain => 5;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://GuZhenRen/images/cards/HuangXingXianYuan.png"
    );

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        await base.OnPlay(choiceContext, cardPlay);

        if (Owner.PlayerCombatState == null)
        {
            return;
        }

        int effectiveHandLimit =
            CardPile.MaxCardsInHand +
            ShaZhaoTuiYan.CountCombatCopies(Owner);

        int cardsToDraw = Math.Max(
            0,
            effectiveHandLimit -
            Owner.PlayerCombatState.Hand.Cards.Count
        );

        if (cardsToDraw <= 0)
        {
            return;
        }

        await CardPileCmd.Draw(
            choiceContext,
            cardsToDraw,
            Owner,
            false
        );
    }
}
