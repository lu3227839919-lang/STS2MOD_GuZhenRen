using GuZhenRen.Cards;

using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Powers.GuangDao;

/// <summary>
/// 折光：追踪同一玩家本回合打出的上一张标准类型卡牌。
/// 当前牌为光道牌且类型改变时获得 1 点光辉，每回合最多获得 3 点。
/// 上一张牌可以来自任意流派。
/// </summary>
[RegisterPower]
public sealed class ZheGuangPower : ModPowerTemplate
{
    private const string PreviousTypeKey = "PreviousCardType";
    private const string GainedThisTurnKey = "GainedThisTurn";
    private const int MaximumGainPerTurn = 3;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar(PreviousTypeKey, (int)CardType.None),
        new DynamicVar(GainedThisTurnKey, 0),
    ];

    public override Task AfterEnergyReset(Player player)
    {
        if (ReferenceEquals(player, Owner.Player))
        {
            DynamicVars[PreviousTypeKey].BaseValue =
                (int)CardType.None;
            DynamicVars[GainedThisTurnKey].BaseValue = 0;
        }

        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        CardModel card = cardPlay.Card;

        if (!ReferenceEquals(card.Owner, Owner.Player) ||
            card.Type is not (
                CardType.Attack or
                CardType.Skill or
                CardType.Power
            ))
        {
            return;
        }

        CardType previous = (CardType)(int)
            DynamicVars[PreviousTypeKey].BaseValue;
        int gainedThisTurn = (int)
            DynamicVars[GainedThisTurnKey].BaseValue;

        if (previous != CardType.None &&
            previous != card.Type &&
            gainedThisTurn < MaximumGainPerTurn &&
            GuangDaoPowerSystem.IsGuangDaoCard(card))
        {
            int gained = await GuangDaoPowerSystem.GainGuangHui(
                choiceContext,
                card,
                1
            );

            if (gained > 0)
            {
                DynamicVars[GainedThisTurnKey].BaseValue += gained;
                Flash();
            }
        }

        // 无论上一张牌属于哪个流派，都作为下一次折光的类型参照。
        DynamicVars[PreviousTypeKey].BaseValue = (int)card.Type;
    }
}
