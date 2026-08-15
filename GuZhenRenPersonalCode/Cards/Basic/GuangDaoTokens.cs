using GuZhenRen.Characters;
using GuZhenRen.Powers.GuangDao;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.GuangDao;

public abstract class AbstractGuangDaoToken
    : AbstractGuZhenRenGeneratedCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Exhaust];

    protected AbstractGuangDaoToken(int baseCost, CardType type)
        : base(
            baseCost,
            type,
            CardRarity.Token,
            TargetType.Self
        )
    {
        SetDao(Dao.GuangDao);
    }

    protected static async Task ApplyFocus(
        PlayerChoiceContext choiceContext,
        CardModel source,
        int amount
    )
    {
        if (amount <= 0)
        {
            return;
        }

        await PowerCmd.Apply<JuGuangPower>(
            choiceContext,
            source.Owner.Creature,
            amount,
            source.Owner.Creature,
            source
        );
    }
}


